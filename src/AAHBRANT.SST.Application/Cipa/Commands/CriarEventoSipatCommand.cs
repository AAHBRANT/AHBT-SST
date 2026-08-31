using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarEventoSipatCommand(
    Guid ObraId,
    int AnoReferencia,
    DateTime DataInicio,
    DateTime DataFim,
    string? Tema,
    string? Programacao) : IRequest<Guid>;

public class CriarEventoSipatCommandValidator : AbstractValidator<CriarEventoSipatCommand>
{
    public CriarEventoSipatCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.AnoReferencia).InclusiveBetween(2000, 2100);
        RuleFor(x => x.DataFim).GreaterThanOrEqualTo(x => x.DataInicio);
        RuleFor(x => x.Tema).MaximumLength(300);
        RuleFor(x => x.Programacao).MaximumLength(4000);
    }
}

public class CriarEventoSipatCommandHandler : IRequestHandler<CriarEventoSipatCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarEventoSipatCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEventoSipatCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var evento = new EventoSipat
        {
            ObraId = request.ObraId,
            AnoReferencia = request.AnoReferencia,
            DataInicio = request.DataInicio,
            DataFim = request.DataFim,
            Tema = request.Tema,
            Programacao = request.Programacao,
        };

        _db.EventosSipat.Add(evento);
        await _db.SaveChangesAsync(ct);
        return evento.Id;
    }
}
