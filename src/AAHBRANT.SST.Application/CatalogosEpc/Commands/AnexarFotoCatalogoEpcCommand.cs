using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record AnexarFotoCatalogoEpcCommand(
    Guid CatalogoEpcId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoCatalogoEpcCommandValidator : AbstractValidator<AnexarFotoCatalogoEpcCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.CatalogoEpcId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoCatalogoEpcCommandHandler : IRequestHandler<AnexarFotoCatalogoEpcCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoCatalogoEpcCommand request, CancellationToken ct)
    {
        var epc = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpcId, ct)
            ?? throw new KeyNotFoundException($"EPC de catálogo {request.CatalogoEpcId} não encontrado.");

        epc.FotoConteudo = request.FotoConteudo;
        epc.FotoContentType = request.FotoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
