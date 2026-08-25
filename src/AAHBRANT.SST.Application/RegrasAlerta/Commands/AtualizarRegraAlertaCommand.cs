using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegrasAlerta.Commands;

public record AtualizarRegraAlertaCommand(
    Guid Id,
    TipoModuloAlerta Modulo,
    int DiasAntecedencia,
    SeveridadeAlerta Severidade,
    Guid? ResponsavelUsuarioId = null) : IRequest;

public class AtualizarRegraAlertaCommandValidator : AbstractValidator<AtualizarRegraAlertaCommand>
{
    public AtualizarRegraAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DiasAntecedencia).GreaterThanOrEqualTo(0);
    }
}

public class AtualizarRegraAlertaCommandHandler : IRequestHandler<AtualizarRegraAlertaCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRegraAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRegraAlertaCommand request, CancellationToken ct)
    {
        var regra = await _db.RegrasAlerta.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Regra de alerta {request.Id} não encontrada.");

        regra.Modulo = request.Modulo;
        regra.DiasAntecedencia = request.DiasAntecedencia;
        regra.Severidade = request.Severidade;
        regra.ResponsavelUsuarioId = request.ResponsavelUsuarioId;

        await _db.SaveChangesAsync(ct);
    }
}
