using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Fotos extras de um item da inspeção (30/10/2026) — ver InspecaoItemFotoAdicional. A foto principal
// (posição 1) continua em AnexarFotoItemInspecaoCommand; aqui fica a posição 2 (máximo de 2 fotos por item, decisão do usuário). Só online.
public record AnexarFotoAdicionalItemInspecaoCommand(
    Guid RespostaId,
    int Ordem,
    byte[] FotoConteudo,
    string FotoContentType,
    string? Metadados = null) : IRequest<Guid>;

public class AnexarFotoAdicionalItemInspecaoCommandValidator : AbstractValidator<AnexarFotoAdicionalItemInspecaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    public const int OrdemMaxima = 2;

    public AnexarFotoAdicionalItemInspecaoCommandValidator()
    {
        RuleFor(x => x.RespostaId).NotEmpty();
        RuleFor(x => x.Ordem).InclusiveBetween(2, OrdemMaxima).WithMessage("Cada item aceita até 2 fotos.");
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoAdicionalItemInspecaoCommandHandler : IRequestHandler<AnexarFotoAdicionalItemInspecaoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public AnexarFotoAdicionalItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarFotoAdicionalItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.Include(r => r.Inspecao).FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, "Inspecao", resposta.InspecaoId, ct);
        if (resposta.Inspecao!.Status == StatusInspecao.Concluida)
            throw new InvalidOperationException("A inspeção já foi finalizada.");
        if (resposta.FotoConteudo.Length == 0)
            throw new InvalidOperationException("Registre a foto principal do item antes das fotos extras.");

        var metadados = await DadosCapturaFoto.PrepararAsync(request.Metadados, resposta.Inspecao.ObraId, _db, ct);

        var existente = await _db.InspecaoItemFotosAdicionais
            .FirstOrDefaultAsync(f => f.InspecaoItemRespostaId == resposta.Id && f.Ordem == request.Ordem, ct);
        if (existente is not null) existente.Ativo = false;

        var foto = new InspecaoItemFotoAdicional
        {
            InspecaoItemRespostaId = resposta.Id,
            Ordem = request.Ordem,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
            FotoMetadadosJson = metadados,
        };
        _db.InspecaoItemFotosAdicionais.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}

public record RemoverFotoAdicionalItemInspecaoCommand(Guid FotoId) : IRequest;

public class RemoverFotoAdicionalItemInspecaoCommandHandler : IRequestHandler<RemoverFotoAdicionalItemInspecaoCommand>
{
    private readonly IAppDbContext _db;
    public RemoverFotoAdicionalItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverFotoAdicionalItemInspecaoCommand request, CancellationToken ct)
    {
        var foto = await _db.InspecaoItemFotosAdicionais.Include(f => f.InspecaoItemResposta).ThenInclude(r => r!.Inspecao)
            .FirstOrDefaultAsync(f => f.Id == request.FotoId, ct)
            ?? throw new KeyNotFoundException("Foto não encontrada.");
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, "Inspecao", foto.InspecaoItemResposta!.InspecaoId, ct);
        if (foto.InspecaoItemResposta.Inspecao!.Status == StatusInspecao.Concluida)
            throw new InvalidOperationException("A inspeção já foi finalizada.");
        foto.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}

public record ObterFotoAdicionalItemInspecaoQuery(Guid FotoId) : IRequest<FotoItemInspecaoResultado?>;

public class ObterFotoAdicionalItemInspecaoQueryHandler : IRequestHandler<ObterFotoAdicionalItemInspecaoQuery, FotoItemInspecaoResultado?>
{
    private readonly IAppDbContext _db;
    public ObterFotoAdicionalItemInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoItemInspecaoResultado?> Handle(ObterFotoAdicionalItemInspecaoQuery request, CancellationToken ct)
    {
        var foto = await _db.InspecaoItemFotosAdicionais.FirstOrDefaultAsync(f => f.Id == request.FotoId, ct);
        if (foto is null) return null;
        if (!await _db.ObraDoPaiNoEscopoAsync(
                _db.InspecaoItemRespostas.IgnoreQueryFilters().Where(r => r.Id == foto.InspecaoItemRespostaId).Select(r => r.Inspecao!.ObraId), ct))
            return null;
        var extensao = foto.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoItemInspecaoResultado
        {
            Conteudo = foto.FotoConteudo,
            ContentType = foto.FotoContentType,
            NomeArquivo = $"inspecao-item-{foto.InspecaoItemRespostaId}-{foto.Ordem}.{extensao}",
        };
    }
}
