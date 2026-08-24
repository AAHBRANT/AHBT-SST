using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

// Fluxo de status proposto (não é citação literal do §34 — a Base de Conhecimento só diz "o sistema
// deve gerar alertas", não define fluxo de tratamento): Aberto → EmTratamento → Escalonado →
// Resolvido/Ignorado. Mesmo princípio de bloqueio preventivo já usado em outros módulos: só é
// possível iniciar tratamento a partir de Aberto.
public record IniciarTratamentoAlertaCommand(Guid Id) : IRequest;

public class IniciarTratamentoAlertaCommandValidator : AbstractValidator<IniciarTratamentoAlertaCommand>
{
    public IniciarTratamentoAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class IniciarTratamentoAlertaCommandHandler : IRequestHandler<IniciarTratamentoAlertaCommand>
{
    private readonly IAppDbContext _db;

    public IniciarTratamentoAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(IniciarTratamentoAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        if (alerta.Status != StatusAlerta.Aberto)
            throw new InvalidOperationException("Só é possível iniciar tratamento de um alerta em status Aberto.");

        alerta.Status = StatusAlerta.EmTratamento;
        await _db.SaveChangesAsync(ct);
    }
}
