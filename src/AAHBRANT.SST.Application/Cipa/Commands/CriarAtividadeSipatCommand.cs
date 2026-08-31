using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarAtividadeSipatCommand(
    Guid EventoSipatId,
    DateTime Data,
    string? Horario,
    string TemaPalestra,
    string? Palestrante) : IRequest<Guid>;

public class CriarAtividadeSipatCommandValidator : AbstractValidator<CriarAtividadeSipatCommand>
{
    public CriarAtividadeSipatCommandValidator()
    {
        RuleFor(x => x.EventoSipatId).NotEmpty();
        RuleFor(x => x.TemaPalestra).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Horario).MaximumLength(50);
        RuleFor(x => x.Palestrante).MaximumLength(200);
    }
}

public class CriarAtividadeSipatCommandHandler : IRequestHandler<CriarAtividadeSipatCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAtividadeSipatCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAtividadeSipatCommand request, CancellationToken ct)
    {
        if (!await _db.EventosSipat.AnyAsync(e => e.Id == request.EventoSipatId, ct))
            throw new KeyNotFoundException($"Evento SIPAT {request.EventoSipatId} não encontrado.");

        var atividade = new AtividadeSipat
        {
            EventoSipatId = request.EventoSipatId,
            Data = request.Data,
            Horario = request.Horario,
            TemaPalestra = request.TemaPalestra,
            Palestrante = request.Palestrante,
        };

        _db.AtividadesSipat.Add(atividade);
        await _db.SaveChangesAsync(ct);
        return atividade.Id;
    }
}
