using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosEpi.Commands;

public record CriarCatalogoEpiCommand(
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses,
    string? CodigoBarras) : IRequest<Guid>;

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
        var epi = new Domain.Entidades.CatalogoEpi
        {
            Nome = request.Nome,
            Fabricante = request.Fabricante,
            CertificadoAprovacaoNumero = request.CertificadoAprovacaoNumero,
            CertificadoAprovacaoValidade = request.CertificadoAprovacaoValidade,
            VidaUtilEmMeses = request.VidaUtilEmMeses,
            CodigoBarras = request.CodigoBarras,
        };
        _db.CatalogoEpis.Add(epi);
        await _db.SaveChangesAsync(ct);
        return epi.Id;
    }
}
