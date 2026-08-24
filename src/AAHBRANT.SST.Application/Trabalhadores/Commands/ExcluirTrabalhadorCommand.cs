using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record ExcluirTrabalhadorCommand(Guid Id) : IRequest;

public class ExcluirTrabalhadorCommandValidator : AbstractValidator<ExcluirTrabalhadorCommand>
{
    public ExcluirTrabalhadorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirTrabalhadorCommandHandler : IRequestHandler<ExcluirTrabalhadorCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.Id} não encontrado.");

        _db.Trabalhadores.Remove(trabalhador);
        await _db.SaveChangesAsync(ct);
    }
}
