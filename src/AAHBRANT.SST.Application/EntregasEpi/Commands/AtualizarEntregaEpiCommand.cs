using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record AtualizarEntregaEpiCommand(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    bool AssinaturaColetada) : IRequest;

public class AtualizarEntregaEpiCommandValidator : AbstractValidator<AtualizarEntregaEpiCommand>
{
    public AtualizarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
    }
}

public class AtualizarEntregaEpiCommandHandler : IRequestHandler<AtualizarEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        entrega.TrabalhadorId = request.TrabalhadorId;
        entrega.CatalogoEpiId = request.CatalogoEpiId;
        entrega.DataEntrega = request.DataEntrega;
        entrega.DataDevolucao = request.DataDevolucao;
        entrega.DataValidade = request.DataValidade;
        entrega.AssinaturaColetada = request.AssinaturaColetada;

        await _db.SaveChangesAsync(ct);
    }
}
