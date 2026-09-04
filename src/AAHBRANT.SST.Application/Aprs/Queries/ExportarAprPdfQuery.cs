using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ExportarAprPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarAprPdfQueryHandler : IRequestHandler<ExportarAprPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IAprPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAprPdfQueryHandler(IMediator mediator, IAppDbContext db, IAprPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAprPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAprDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        byte[]? logoConteudo = detalhe.Apr.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Apr), request.Id, ct);

        return _pdf.Gerar(MontarModelo(detalhe, logoConteudo, rastreio));
    }

    public static AprPdfModelo MontarModelo(AprDetalheDto detalhe, byte[]? obraLogoConteudo, RastreabilidadeDocumentoResultado rastreio)
    {
        var assinaturasPorTrabalhador = detalhe.Assinaturas
            .Where(a => a.Papel == PapelAssinaturaApr.Envolvido)
            .Select(a => a.TrabalhadorId)
            .ToHashSet();

        var envolvidos = detalhe.Responsaveis
            .Select(r => new AprPdfEnvolvido(r.TrabalhadorNome, r.TrabalhadorFuncaoNome, assinaturasPorTrabalhador.Contains(r.TrabalhadorId)))
            .ToList();

        var riscos = detalhe.Etapas
            .SelectMany(e => e.Riscos.Select(r => new AprPdfRiscoLinha(
                e.Descricao,
                r.PerigoEventoPerigoso,
                r.FonteCircunstancia,
                r.PossiveisLesoes,
                r.TrabalhadoresExpostos,
                r.ProbabilidadeInicial,
                r.SeveridadeInicial,
                r.NivelRiscoInicial,
                r.MedidasPrevencao,
                r.Responsavel,
                r.ProbabilidadeResidual,
                r.SeveridadeResidual,
                r.NivelRiscoResidual)))
            .ToList();

        var elaboracao = detalhe.Assinaturas.Where(a => a.Papel == PapelAssinaturaApr.Elaboracao)
            .OrderByDescending(a => a.DataAssinatura).FirstOrDefault();
        var supervisao = detalhe.Assinaturas.Where(a => a.Papel == PapelAssinaturaApr.Supervisao)
            .OrderByDescending(a => a.DataAssinatura).FirstOrDefault();

        return new AprPdfModelo(
            detalhe.Apr.NumeroApr,
            detalhe.Apr.ObraNome,
            obraLogoConteudo,
            detalhe.Apr.AtividadeNome,
            detalhe.Apr.Local,
            detalhe.Apr.MaquinasEquipamentos,
            detalhe.Apr.PgrReferencia,
            detalhe.Apr.Data,
            envolvidos,
            riscos,
            new AprPdfAssinatura(elaboracao?.TrabalhadorNome, elaboracao?.TrabalhadorFuncaoNome, elaboracao?.DataAssinatura),
            new AprPdfAssinatura(supervisao?.TrabalhadorNome, supervisao?.TrabalhadorFuncaoNome, supervisao?.DataAssinatura),
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            // TemAssinatura vem da tabela própria AprAssinatura (Motor de Assinatura Eletrônica não é
            // usado pela APR) — rastreio.TemAssinatura é deliberadamente ignorado aqui.
            detalhe.Assinaturas.Count > 0);
    }
}
