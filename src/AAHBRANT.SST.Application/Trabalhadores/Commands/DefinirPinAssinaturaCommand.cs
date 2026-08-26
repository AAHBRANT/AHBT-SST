using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Define/troca o PIN do método de reserva do Motor de Assinatura Eletrônica (crachá/QR + PIN — ver
// CrachaPinAutenticacaoStrategy). Sem esta ação o trabalhador nunca consegue assinar por este método,
// mesmo com crachá vinculado e Termo de Aceite confirmado.
public record DefinirPinAssinaturaCommand(Guid TrabalhadorId, string Pin, string ConfirmarPin) : IRequest;

public class DefinirPinAssinaturaCommandValidator : AbstractValidator<DefinirPinAssinaturaCommand>
{
    public DefinirPinAssinaturaCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4,6}$").WithMessage("O PIN deve ter de 4 a 6 dígitos numéricos.");
        RuleFor(x => x.ConfirmarPin).Equal(x => x.Pin).WithMessage("A confirmação do PIN não corresponde ao PIN informado.");
    }
}

public class DefinirPinAssinaturaCommandHandler : IRequestHandler<DefinirPinAssinaturaCommand>
{
    private readonly IAppDbContext _db;
    private readonly IPinHasher _pinHasher;

    public DefinirPinAssinaturaCommandHandler(IAppDbContext db, IPinHasher pinHasher)
    {
        _db = db;
        _pinHasher = pinHasher;
    }

    public async Task Handle(DefinirPinAssinaturaCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        trabalhador.PinHash = _pinHasher.GerarHash(request.Pin);
        await _db.SaveChangesAsync(ct);
    }
}
