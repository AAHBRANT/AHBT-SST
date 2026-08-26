using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Consentimento LGPD específico para tratamento de dado biométrico (docs/Motor-Assinatura-Eletronica.md
// §4, item 2 — art. 5º II e art. 11 da LGPD). Deliberadamente um comando separado de
// RegistrarTermoAceiteAssinaturaCommand: dado biométrico é sensível e exige base legal própria, não
// coberta pelo consentimento geral de assinatura eletrônica — o trabalhador pode aceitar assinar
// eletronicamente e mesmo assim recusar este consentimento, permanecendo no método de reserva
// (crachá+PIN).
public record RegistrarConsentimentoBiometriaCommand(Guid TrabalhadorId) : IRequest;

public class RegistrarConsentimentoBiometriaCommandValidator : AbstractValidator<RegistrarConsentimentoBiometriaCommand>
{
    public RegistrarConsentimentoBiometriaCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
    }
}

public class RegistrarConsentimentoBiometriaCommandHandler : IRequestHandler<RegistrarConsentimentoBiometriaCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarConsentimentoBiometriaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarConsentimentoBiometriaCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        trabalhador.ConsentimentoBiometriaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
