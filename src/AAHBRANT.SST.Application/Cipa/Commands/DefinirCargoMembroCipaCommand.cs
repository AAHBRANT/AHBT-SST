using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Ajusta o cargo de um membro já cadastrado (ex.: eleger Presidente/Vice-Presidente/Secretário
// entre os titulares, após a apuração — a NR-5 não define esse papel via voto direto do sistema).
public record DefinirCargoMembroCipaCommand(Guid MembroId, CargoMembroCipa Cargo) : IRequest;

public class DefinirCargoMembroCipaCommandValidator : AbstractValidator<DefinirCargoMembroCipaCommand>
{
    public DefinirCargoMembroCipaCommandValidator() => RuleFor(x => x.MembroId).NotEmpty();
}

public class DefinirCargoMembroCipaCommandHandler : IRequestHandler<DefinirCargoMembroCipaCommand>
{
    private readonly IAppDbContext _db;

    public DefinirCargoMembroCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirCargoMembroCipaCommand request, CancellationToken ct)
    {
        var membro = await _db.MembrosCipa.FirstOrDefaultAsync(m => m.Id == request.MembroId, ct)
            ?? throw new KeyNotFoundException($"Membro {request.MembroId} não encontrado.");

        membro.Cargo = request.Cargo;
        await _db.SaveChangesAsync(ct);
    }
}
