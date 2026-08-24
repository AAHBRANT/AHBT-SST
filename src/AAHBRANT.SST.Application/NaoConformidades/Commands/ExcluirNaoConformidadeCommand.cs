using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

public record ExcluirNaoConformidadeCommand(Guid Id) : IRequest;

public class ExcluirNaoConformidadeCommandValidator : AbstractValidator<ExcluirNaoConformidadeCommand>
{
    public ExcluirNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirNaoConformidadeCommandHandler : IRequestHandler<ExcluirNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        _db.NaoConformidades.Remove(nc);
        await _db.SaveChangesAsync(ct);
    }
}
