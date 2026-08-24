using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record ExcluirAcidenteCommand(Guid Id) : IRequest;

public class ExcluirAcidenteCommandValidator : AbstractValidator<ExcluirAcidenteCommand>
{
    public ExcluirAcidenteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAcidenteCommandHandler : IRequestHandler<ExcluirAcidenteCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAcidenteCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        _db.Acidentes.Remove(acidente);
        await _db.SaveChangesAsync(ct);
    }
}
