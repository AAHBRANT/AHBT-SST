using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Termo de Aceite de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §4, item 1) — cobre
// tanto biometria quanto o método de reserva (crachá+PIN); é o único dos dois consentimentos exigido
// para assinar por crachá+PIN (ConsentimentoBiometriaEm só é exigido pela estratégia biométrica,
// ver RegistrarConsentimentoBiometriaCommand). Reafirmar substitui a data anterior — não é idempotente
// por design, cada confirmação é um novo evento de aceite.
public record RegistrarTermoAceiteAssinaturaCommand(Guid TrabalhadorId) : IRequest;

public class RegistrarTermoAceiteAssinaturaCommandValidator : AbstractValidator<RegistrarTermoAceiteAssinaturaCommand>
{
    public RegistrarTermoAceiteAssinaturaCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
    }
}

public class RegistrarTermoAceiteAssinaturaCommandHandler : IRequestHandler<RegistrarTermoAceiteAssinaturaCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarTermoAceiteAssinaturaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarTermoAceiteAssinaturaCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        trabalhador.TermoAceiteAssinaturaEletronicaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
