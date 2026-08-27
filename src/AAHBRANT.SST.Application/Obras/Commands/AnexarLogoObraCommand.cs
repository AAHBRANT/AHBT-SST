using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record AnexarLogoObraCommand(
    Guid ObraId,
    byte[] LogoConteudo,
    string LogoContentType) : IRequest;

public class AnexarLogoObraCommandValidator : AbstractValidator<AnexarLogoObraCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarLogoObraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.LogoConteudo)
            .NotEmpty().WithMessage("O logo é obrigatório.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("O logo deve ter no máximo 5 MB.");
        RuleFor(x => x.LogoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("O logo deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarLogoObraCommandHandler : IRequestHandler<AnexarLogoObraCommand>
{
    private readonly IAppDbContext _db;

    public AnexarLogoObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarLogoObraCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.ObraId, ct)
            ?? throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        obra.LogoConteudo = request.LogoConteudo;
        obra.LogoContentType = request.LogoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
