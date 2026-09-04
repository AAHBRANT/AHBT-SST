using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Commands;

// Foto do item do catálogo (pedido do usuário, 03/09) — mesmo padrão de AnexarFotoTrabalhadorCommand/
// AnexarLogoObraCommand: binário guardado direto na linha do CatalogoEpi, upload separado da
// criação/edição do cadastro (o front chama isso logo em seguida de criar, num único fluxo de tela).
public record AnexarFotoCatalogoEpiCommand(
    Guid CatalogoEpiId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoCatalogoEpiCommandValidator : AbstractValidator<AnexarFotoCatalogoEpiCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoCatalogoEpiCommandValidator()
    {
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoCatalogoEpiCommandHandler : IRequestHandler<AnexarFotoCatalogoEpiCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoCatalogoEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoCatalogoEpiCommand request, CancellationToken ct)
    {
        var epi = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpiId, ct)
            ?? throw new KeyNotFoundException($"EPI de catálogo {request.CatalogoEpiId} não encontrado.");

        epi.FotoConteudo = request.FotoConteudo;
        epi.FotoContentType = request.FotoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
