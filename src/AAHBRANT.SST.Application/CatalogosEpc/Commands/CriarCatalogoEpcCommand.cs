using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CatalogosEpc.Commands;

public record CriarCatalogoEpcCommand(
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses) : IRequest<Guid>;

public class CriarCatalogoEpcCommandValidator : AbstractValidator<CriarCatalogoEpcCommand>
{
    public CriarCatalogoEpcCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.VidaUtilEmMeses).GreaterThan(0);
    }
}

public class CriarCatalogoEpcCommandHandler : IRequestHandler<CriarCatalogoEpcCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCatalogoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoEpcCommand request, CancellationToken ct)
    {
        var epc = new Domain.Entidades.CatalogoEpc
        {
            Nome = request.Nome,
            Fabricante = request.Fabricante,
            CertificadoAprovacaoNumero = request.CertificadoAprovacaoNumero,
            CertificadoAprovacaoValidade = request.CertificadoAprovacaoValidade,
            VidaUtilEmMeses = request.VidaUtilEmMeses,
        };
        _db.CatalogoEpcs.Add(epc);
        await _db.SaveChangesAsync(ct);
        return epc.Id;
    }
}
