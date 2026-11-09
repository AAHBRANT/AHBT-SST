using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record CriarCameraCommand(
    Guid ObraId,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos) : IRequest<Guid>;

public class CriarCameraCommandValidator : AbstractValidator<CriarCameraCommand>
{
    public CriarCameraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
    }
}

public class CriarCameraCommandHandler : IRequestHandler<CriarCameraCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCameraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == request.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.ObraId == request.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        var camera = new Camera
        {
            ObraId = request.ObraId,
            Nome = nome,
            AreaSstId = request.AreaSstId,
            Tipo = request.Tipo,
            Habilitada = request.Habilitada,
            IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos,
        };
        _db.Cameras.Add(camera);
        await _db.SaveChangesAsync(ct);
        return camera.Id;
    }
}
