using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarReuniaoCipaCommand(
    Guid ObraId,
    TipoReuniaoCipa Tipo,
    DateTime DataReuniao,
    string? Pauta) : IRequest<Guid>;

public class CriarReuniaoCipaCommandValidator : AbstractValidator<CriarReuniaoCipaCommand>
{
    public CriarReuniaoCipaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Pauta).MaximumLength(2000);
    }
}

public class CriarReuniaoCipaCommandHandler : IRequestHandler<CriarReuniaoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarReuniaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarReuniaoCipaCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var reuniao = new ReuniaoCipa
        {
            ObraId = request.ObraId,
            Tipo = request.Tipo,
            DataReuniao = request.DataReuniao,
            Pauta = request.Pauta,
            Status = StatusReuniaoCipa.Agendada,
        };

        _db.ReunioesCipa.Add(reuniao);
        await _db.SaveChangesAsync(ct);
        return reuniao.Id;
    }
}
