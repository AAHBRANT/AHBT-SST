using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// A obra não muda na edição: as ocorrências já gravadas carregam o ObraId da câmera.
public record AtualizarCameraCommand(
    Guid Id,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos) : IRequest;

public class AtualizarCameraCommandValidator : AbstractValidator<AtualizarCameraCommand>
{
    public AtualizarCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
    }
}

public class AtualizarCameraCommandHandler : IRequestHandler<AtualizarCameraCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.Id} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == camera.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.Id != camera.Id && c.ObraId == camera.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        camera.Nome = nome;
        camera.AreaSstId = request.AreaSstId;
        camera.Tipo = request.Tipo;
        camera.Habilitada = request.Habilitada;
        camera.IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos;
        await _db.SaveChangesAsync(ct);
    }
}
