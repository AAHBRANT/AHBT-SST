using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarInspecaoCipaCommand(
    Guid ObraId,
    Guid? MembroCipaId,
    DateTime Data,
    string Local,
    string RiscoIdentificado,
    NivelRisco? GrauRisco) : IRequest<Guid>;

public class CriarInspecaoCipaCommandValidator : AbstractValidator<CriarInspecaoCipaCommand>
{
    public CriarInspecaoCipaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RiscoIdentificado).NotEmpty().MaximumLength(1000);
    }
}

public class CriarInspecaoCipaCommandHandler : IRequestHandler<CriarInspecaoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarInspecaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarInspecaoCipaCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.MembroCipaId.HasValue && !await _db.MembrosCipa.AnyAsync(m => m.Id == request.MembroCipaId, ct))
            throw new KeyNotFoundException($"Membro da CIPA {request.MembroCipaId} não encontrado.");

        var inspecao = new InspecaoCipa
        {
            ObraId = request.ObraId,
            MembroCipaId = request.MembroCipaId,
            Data = request.Data,
            Local = request.Local,
            RiscoIdentificado = request.RiscoIdentificado,
            GrauRisco = request.GrauRisco,
        };

        _db.InspecoesCipa.Add(inspecao);
        await _db.SaveChangesAsync(ct);
        return inspecao.Id;
    }
}
