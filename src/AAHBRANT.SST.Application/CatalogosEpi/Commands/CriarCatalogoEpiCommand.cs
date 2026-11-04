using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Commands;

public record CriarCatalogoEpiCommand(
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses,
    Guid? ObraId = null) : IRequest<Guid>;

public class CriarCatalogoEpiCommandValidator : AbstractValidator<CriarCatalogoEpiCommand>
{
    public CriarCatalogoEpiCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.VidaUtilEmMeses).GreaterThan(0);
    }
}

public class CriarCatalogoEpiCommandHandler : IRequestHandler<CriarCatalogoEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoEpiCommand request, CancellationToken ct)
    {
        // Item da obra (04/11/2026): a obra precisa existir e estar no escopo de quem cadastra (o filtro de obra já cuida disso).
        if (request.ObraId is { } obraId && !await _db.Obras.AnyAsync(o => o.Id == obraId, ct))
            throw new KeyNotFoundException("Obra não encontrada ou sem acesso.");

        var epi = new Domain.Entidades.CatalogoEpi
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            Fabricante = request.Fabricante,
            CertificadoAprovacaoNumero = request.CertificadoAprovacaoNumero,
            CertificadoAprovacaoValidade = request.CertificadoAprovacaoValidade,
            VidaUtilEmMeses = request.VidaUtilEmMeses,
        };
        _db.CatalogoEpis.Add(epi);
        await _db.SaveChangesAsync(ct);
        return epi.Id;
    }
}
