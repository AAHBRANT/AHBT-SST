using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record ParticipanteReuniaoCipaEntrada(Guid TrabalhadorId, bool Presente);

// Substitui a lista de presença inteira da reunião a cada chamada (idempotente) — mais simples que
// reconciliar incrementalmente, e a tela sempre envia a lista completa marcada.
public record RegistrarParticipantesReuniaoCipaCommand(Guid ReuniaoId, List<ParticipanteReuniaoCipaEntrada> Participantes) : IRequest;

public class RegistrarParticipantesReuniaoCipaCommandValidator : AbstractValidator<RegistrarParticipantesReuniaoCipaCommand>
{
    public RegistrarParticipantesReuniaoCipaCommandValidator()
    {
        RuleFor(x => x.ReuniaoId).NotEmpty();
        RuleFor(x => x.Participantes).NotEmpty();
    }
}

public class RegistrarParticipantesReuniaoCipaCommandHandler : IRequestHandler<RegistrarParticipantesReuniaoCipaCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarParticipantesReuniaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarParticipantesReuniaoCipaCommand request, CancellationToken ct)
    {
        var reuniao = await _db.ReunioesCipa.FirstOrDefaultAsync(r => r.Id == request.ReuniaoId, ct)
            ?? throw new KeyNotFoundException($"Reunião {request.ReuniaoId} não encontrada.");

        var existentes = await _db.ParticipantesReuniaoCipa
            .Where(p => p.ReuniaoCipaId == request.ReuniaoId && p.Ativo)
            .ToListAsync(ct);

        foreach (var existente in existentes)
            _db.ParticipantesReuniaoCipa.Remove(existente);

        foreach (var entrada in request.Participantes)
        {
            _db.ParticipantesReuniaoCipa.Add(new ParticipanteReuniaoCipa
            {
                ReuniaoCipaId = request.ReuniaoId,
                TrabalhadorId = entrada.TrabalhadorId,
                Presente = entrada.Presente,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
