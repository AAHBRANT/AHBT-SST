using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmso.Commands;

public record CriarPcmsoCommand(
    Guid ObraId,
    string Nome,
    string? Objetivo,
    string MedicoCoordenadorNome,
    string? MedicoCoordenadorCrm,
    Guid? MedicoCoordenadorUsuarioId,
    DateTime DataElaboracao,
    DateTime? DataVigenciaInicio,
    DateTime? DataVigenciaFim,
    StatusPcmso Status) : IRequest<Guid>;

public class CriarPcmsoCommandValidator : AbstractValidator<CriarPcmsoCommand>
{
    public CriarPcmsoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MedicoCoordenadorNome).NotEmpty().MaximumLength(200);
    }
}

public class CriarPcmsoCommandHandler : IRequestHandler<CriarPcmsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPcmsoCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var pcmso = new Domain.Entidades.Pcmso
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            Objetivo = request.Objetivo,
            MedicoCoordenadorNome = request.MedicoCoordenadorNome,
            MedicoCoordenadorCrm = request.MedicoCoordenadorCrm,
            MedicoCoordenadorUsuarioId = request.MedicoCoordenadorUsuarioId,
            DataElaboracao = request.DataElaboracao,
            DataVigenciaInicio = request.DataVigenciaInicio,
            DataVigenciaFim = request.DataVigenciaFim,
            Status = request.Status,
        };

        _db.Pcmsos.Add(pcmso);
        await _db.SaveChangesAsync(ct);
        return pcmso.Id;
    }
}
