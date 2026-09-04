using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpi.Commands;

public record AtualizarCatalogoEpiCommand(
    Guid Id,
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses,
    string? CodigoBarras) : IRequest;

public class AtualizarCatalogoEpiCommandValidator : AbstractValidator<AtualizarCatalogoEpiCommand>
{
    public AtualizarCatalogoEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.VidaUtilEmMeses).GreaterThan(0);
    }
}

public class AtualizarCatalogoEpiCommandHandler : IRequestHandler<AtualizarCatalogoEpiCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCatalogoEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoEpiCommand request, CancellationToken ct)
    {
        var epi = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("EPI de catálogo não encontrado.");

        epi.Nome = request.Nome;
        epi.Fabricante = request.Fabricante;
        epi.CertificadoAprovacaoNumero = request.CertificadoAprovacaoNumero;
        epi.CertificadoAprovacaoValidade = request.CertificadoAprovacaoValidade;
        epi.VidaUtilEmMeses = request.VidaUtilEmMeses;
        epi.CodigoBarras = request.CodigoBarras;

        await _db.SaveChangesAsync(ct);
    }
}
