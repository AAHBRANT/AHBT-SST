using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record CriarEntregaEpiCommand(
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    bool AssinaturaColetada) : IRequest<Guid>;

public class CriarEntregaEpiCommandValidator : AbstractValidator<CriarEntregaEpiCommand>
{
    public CriarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
    }
}

public class CriarEntregaEpiCommandHandler : IRequestHandler<CriarEntregaEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = new EntregaEpi
        {
            TrabalhadorId = request.TrabalhadorId,
            CatalogoEpiId = request.CatalogoEpiId,
            DataEntrega = request.DataEntrega,
            DataDevolucao = request.DataDevolucao,
            DataValidade = request.DataValidade,
            AssinaturaColetada = request.AssinaturaColetada,
        };
        _db.EntregasEpi.Add(entrega);
        await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }
}
