using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Cadastro direto de membro — usado para representantes indicados pelo empregador (Origem=Empregador,
// sem candidatura/eleição) e, se necessário, para ajustes manuais de composição. Membros eleitos são
// criados automaticamente pela apuração (RegistrarApuracaoProcessoEleitoralCipaCommand).
public record CriarMembroCipaCommand(
    Guid ObraId,
    Guid TrabalhadorId,
    OrigemMembroCipa OrigemMembro,
    CargoMembroCipa Cargo,
    DateTime DataInicioMandato,
    DateTime DataFimMandato) : IRequest<Guid>;

public class CriarMembroCipaCommandValidator : AbstractValidator<CriarMembroCipaCommand>
{
    public CriarMembroCipaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataFimMandato).GreaterThan(x => x.DataInicioMandato);
    }
}

public class CriarMembroCipaCommandHandler : IRequestHandler<CriarMembroCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarMembroCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarMembroCipaCommand request, CancellationToken ct)
    {
        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId && t.ObraId == request.ObraId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado ou não pertence a esta obra.");

        var membro = new MembroCipa
        {
            ObraId = request.ObraId,
            TrabalhadorId = request.TrabalhadorId,
            OrigemMembro = request.OrigemMembro,
            Cargo = request.Cargo,
            DataInicioMandato = request.DataInicioMandato,
            DataFimMandato = request.DataFimMandato,
        };

        _db.MembrosCipa.Add(membro);
        await _db.SaveChangesAsync(ct);
        return membro.Id;
    }
}
