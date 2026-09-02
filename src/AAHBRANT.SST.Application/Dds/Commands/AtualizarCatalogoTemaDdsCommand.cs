using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record AtualizarCatalogoTemaDdsCommand(Guid Id, string Nome, string? Descricao) : IRequest;

public class AtualizarCatalogoTemaDdsCommandValidator : AbstractValidator<AtualizarCatalogoTemaDdsCommand>
{
    public AtualizarCatalogoTemaDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public class AtualizarCatalogoTemaDdsCommandHandler : IRequestHandler<AtualizarCatalogoTemaDdsCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarCatalogoTemaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoTemaDdsCommand request, CancellationToken ct)
    {
        var tema = await _db.CatalogosTemaDds.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Tema de DDS não encontrado.");

        tema.Nome = request.Nome;
        tema.Descricao = request.Descricao;
        await _db.SaveChangesAsync(ct);
    }
}
