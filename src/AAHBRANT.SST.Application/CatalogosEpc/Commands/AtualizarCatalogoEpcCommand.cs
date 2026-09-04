using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record AtualizarCatalogoEpcCommand(
    Guid Id,
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses) : IRequest;

public class AtualizarCatalogoEpcCommandValidator : AbstractValidator<AtualizarCatalogoEpcCommand>
{
    public AtualizarCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.VidaUtilEmMeses).GreaterThan(0);
    }
}

public class AtualizarCatalogoEpcCommandHandler : IRequestHandler<AtualizarCatalogoEpcCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCatalogoEpcCommand request, CancellationToken ct)
    {
        var epc = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("EPC de catálogo não encontrado.");

        epc.Nome = request.Nome;
        epc.Fabricante = request.Fabricante;
        epc.CertificadoAprovacaoNumero = request.CertificadoAprovacaoNumero;
        epc.CertificadoAprovacaoValidade = request.CertificadoAprovacaoValidade;
        epc.VidaUtilEmMeses = request.VidaUtilEmMeses;

        await _db.SaveChangesAsync(ct);
    }
}
