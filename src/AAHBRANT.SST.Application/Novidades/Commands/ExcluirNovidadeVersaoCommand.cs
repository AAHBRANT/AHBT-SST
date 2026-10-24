using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Novidades.Commands;

public record ExcluirNovidadeVersaoCommand(Guid Id) : IRequest;

public class ExcluirNovidadeVersaoCommandValidator : AbstractValidator<ExcluirNovidadeVersaoCommand>
{
    public ExcluirNovidadeVersaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirNovidadeVersaoCommandHandler : IRequestHandler<ExcluirNovidadeVersaoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirNovidadeVersaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirNovidadeVersaoCommand request, CancellationToken ct)
    {
        var novidade = await _db.NovidadesVersao.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Novidade {request.Id} não encontrada.");

        _db.NovidadesVersao.Remove(novidade);
        await _db.SaveChangesAsync(ct);
    }
}
