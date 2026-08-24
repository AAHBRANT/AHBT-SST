using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AcoesPlano.Commands;

public record ExcluirAcaoPlanoCommand(Guid Id) : IRequest;

public class ExcluirAcaoPlanoCommandValidator : AbstractValidator<ExcluirAcaoPlanoCommand>
{
    public ExcluirAcaoPlanoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAcaoPlanoCommandHandler : IRequestHandler<ExcluirAcaoPlanoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAcaoPlanoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAcaoPlanoCommand request, CancellationToken ct)
    {
        var acao = await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ação de plano {request.Id} não encontrada.");

        _db.AcoesPlano.Remove(acao);
        await _db.SaveChangesAsync(ct);
    }
}
