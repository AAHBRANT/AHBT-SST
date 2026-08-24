using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Commands;

public record ExcluirTreinamentoCommand(Guid Id) : IRequest;

public class ExcluirTreinamentoCommandValidator : AbstractValidator<ExcluirTreinamentoCommand>
{
    public ExcluirTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirTreinamentoCommandHandler : IRequestHandler<ExcluirTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirTreinamentoCommand request, CancellationToken ct)
    {
        var treinamento = await _db.Treinamentos.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Treinamento não encontrado.");

        _db.Treinamentos.Remove(treinamento);
        await _db.SaveChangesAsync(ct);
    }
}
