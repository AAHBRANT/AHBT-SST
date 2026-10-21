using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesEpi.Minimo;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Reposicao;

public record SolicitacaoReposicaoEpiResumoDto(Guid Id, string NumeroDocumento, DateTime CriadaEm, string SolicitanteNome, int TotalItens, bool Assinada);

// Gera a solicitação com tudo que está abaixo do mínimo (ou zerado) na obra e assina com um clique
// (sessão logada, mesmo método da assinatura do DDS semanal). Usuário sem cadastro de trabalhador
// vinculado gera a solicitação mesmo assim, sem assinatura — o PDF mostra "Aguardando assinatura".
public record CriarSolicitacaoReposicaoEpiCommand(Guid ObraId, string? Observacao, string? AzureAdObjectId = null, string? IpAddress = null) : IRequest<Guid>;

public class CriarSolicitacaoReposicaoEpiCommandValidator : AbstractValidator<CriarSolicitacaoReposicaoEpiCommand>
{
    public CriarSolicitacaoReposicaoEpiCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Observacao).MaximumLength(500);
    }
}

public class CriarSolicitacaoReposicaoEpiCommandHandler : IRequestHandler<CriarSolicitacaoReposicaoEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;
    private readonly IMediator _mediator;
    private readonly IRegistradorAssinaturaService _registrador;

    public CriarSolicitacaoReposicaoEpiCommandHandler(
        IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero, IMediator mediator, IRegistradorAssinaturaService registrador)
    {
        _db = db;
        _geradorNumero = geradorNumero;
        _mediator = mediator;
        _registrador = registrador;
    }

    public async Task<Guid> Handle(CriarSolicitacaoReposicaoEpiCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => new { o.PrazoReposicaoEpiDias }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");
        var itens = (await CalculadoraEstoqueEpi.CalcularAsync(_db, request.ObraId, ct, comTransferencias: false))
            .Where(i => SituacaoEstoqueEpi.PedeReposicao(i.Situacao) && i.QuantidadeSugeridaCompra > 0)
            .ToList();
        if (itens.Count == 0)
            throw new InvalidOperationException("Nenhum EPI desta obra está abaixo do mínimo ou em falta agora.");

        var usuario = string.IsNullOrWhiteSpace(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);

        var solicitacao = new SolicitacaoReposicaoEpi
        {
            ObraId = request.ObraId,
            NumeroDocumento = await _geradorNumero.GerarAsync("REP-EPI", ct),
            SolicitanteUsuarioId = usuario?.Id,
            SolicitanteNome = usuario?.Nome ?? "Usuário do sistema",
            PrazoReposicaoDias = obra.PrazoReposicaoEpiDias,
            Observacao = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
        };
        foreach (var item in itens)
        {
            solicitacao.Itens.Add(new SolicitacaoReposicaoEpiItem
            {
                SolicitacaoReposicaoEpiId = solicitacao.Id,
                CatalogoEpiId = item.CatalogoEpiId,
                EpiNome = item.Nome,
                CertificadoAprovacaoNumero = item.CertificadoAprovacaoNumero,
                Saldo = item.Saldo,
                Reservado = item.Reservado,
                EstoqueMinimo = item.EstoqueMinimo ?? 0,
                QuantidadeSugerida = item.QuantidadeSugeridaCompra,
                TamanhosEmFalta = item.TamanhosEmFalta.Count > 0 ? string.Join(", ", item.TamanhosEmFalta) : null,
            });
        }
        _db.SolicitacoesReposicaoEpi.Add(solicitacao);
        await _db.SaveChangesAsync(ct);

        if (usuario?.TrabalhadorId is { } trabalhadorId)
        {
            var documentoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(SolicitacaoReposicaoEpi), solicitacao.Id), ct);
            await _registrador.RegistrarAsync(documentoId,
                new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.SessaoLogada), request.IpAddress, ct);
        }
        return solicitacao.Id;
    }
}

public record ListarSolicitacoesReposicaoEpiQuery(Guid ObraId) : IRequest<List<SolicitacaoReposicaoEpiResumoDto>>;

public class ListarSolicitacoesReposicaoEpiQueryHandler : IRequestHandler<ListarSolicitacoesReposicaoEpiQuery, List<SolicitacaoReposicaoEpiResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarSolicitacoesReposicaoEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SolicitacaoReposicaoEpiResumoDto>> Handle(ListarSolicitacoesReposicaoEpiQuery request, CancellationToken ct)
    {
        var solicitacoes = await _db.SolicitacoesReposicaoEpi
            .Where(s => s.ObraId == request.ObraId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(20)
            .Select(s => new { s.Id, s.NumeroDocumento, s.CreatedAtUtc, s.SolicitanteNome, Total = s.Itens.Count })
            .ToListAsync(ct);
        var ids = solicitacoes.Select(s => s.Id).ToList();
        var assinadas = await _db.DocumentoSignatarios
            .Where(d => d.DocumentoAssinatura!.EntidadeTipo == nameof(SolicitacaoReposicaoEpi) && ids.Contains(d.DocumentoAssinatura.EntidadeId))
            .Select(d => d.DocumentoAssinatura!.EntidadeId)
            .ToListAsync(ct);
        return solicitacoes
            .Select(s => new SolicitacaoReposicaoEpiResumoDto(s.Id, s.NumeroDocumento, s.CreatedAtUtc, s.SolicitanteNome, s.Total, assinadas.Contains(s.Id)))
            .ToList();
    }
}

public record ExportarSolicitacaoReposicaoEpiPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarSolicitacaoReposicaoEpiPdfQueryHandler : IRequestHandler<ExportarSolicitacaoReposicaoEpiPdfQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly ISolicitacaoReposicaoEpiPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarSolicitacaoReposicaoEpiPdfQueryHandler(IAppDbContext db, ISolicitacaoReposicaoEpiPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarSolicitacaoReposicaoEpiPdfQuery request, CancellationToken ct)
    {
        var s = await _db.SolicitacoesReposicaoEpi
            .Include(x => x.Itens)
            .Include(x => x.Obra)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (s is null) return null;

        var assinatura = await _db.DocumentoSignatarios
            .Where(d => d.DocumentoAssinatura!.EntidadeTipo == nameof(SolicitacaoReposicaoEpi) && d.DocumentoAssinatura.EntidadeId == s.Id)
            .Select(d => new { d.AssinadoEm, d.MetodoAutenticacao, Funcao = d.Trabalhador!.Funcao!.Nome })
            .FirstOrDefaultAsync(ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(SolicitacaoReposicaoEpi), s.Id, ct);

        var pdf = _pdf.Gerar(new SolicitacaoReposicaoEpiPdfModelo(
            s.Obra?.Nome ?? "", s.Obra?.LogoConteudo, s.NumeroDocumento, s.CreatedAtUtc, s.SolicitanteNome, assinatura?.Funcao,
            s.PrazoReposicaoDias, s.Observacao,
            s.Itens.OrderBy(i => i.EpiNome)
                .Select(i => new SolicitacaoReposicaoEpiItemPdfModelo(i.EpiNome, i.CertificadoAprovacaoNumero, i.Saldo, i.Reservado, i.EstoqueMinimo, i.QuantidadeSugerida, i.TamanhosEmFalta))
                .ToList(),
            assinatura?.AssinadoEm, assinatura?.MetodoAutenticacao,
            rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng));
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }
}
