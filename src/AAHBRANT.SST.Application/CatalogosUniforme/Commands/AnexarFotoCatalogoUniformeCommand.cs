using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosUniforme.Commands;

// Foto do item do catálogo (decisão do usuário, 2026-09-07) — mesmo padrão de
// AnexarFotoCatalogoEpiCommand: binário guardado direto na linha do CatalogoUniforme, upload
// separado da criação/edição do cadastro.
public record AnexarFotoCatalogoUniformeCommand(
    Guid CatalogoUniformeId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoCatalogoUniformeCommandValidator : AbstractValidator<AnexarFotoCatalogoUniformeCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoCatalogoUniformeCommandValidator()
    {
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoCatalogoUniformeCommandHandler : IRequestHandler<AnexarFotoCatalogoUniformeCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoCatalogoUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoCatalogoUniformeCommand request, CancellationToken ct)
    {
        var item = await _db.CatalogoUniformes.FirstOrDefaultAsync(x => x.Id == request.CatalogoUniformeId, ct)
            ?? throw new KeyNotFoundException($"Peça de catálogo de uniforme {request.CatalogoUniformeId} não encontrada.");

        item.FotoConteudo = request.FotoConteudo;
        item.FotoContentType = request.FotoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
