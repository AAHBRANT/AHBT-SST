using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Commands;

// Entrada manual de estoque (compra/reposição) numa Obra — Fase 3. Cria a linha de EstoqueEpi se
// ainda não existir (primeira entrada desse EPI nessa Obra).
public record RegistrarEntradaEstoqueEpiCommand(
    Guid CatalogoEpiId,
    Guid ObraId,
    int Quantidade,
    string? Observacao) : IRequest;

public class RegistrarEntradaEstoqueEpiCommandValidator : AbstractValidator<RegistrarEntradaEstoqueEpiCommand>
{
    public RegistrarEntradaEstoqueEpiCommandValidator()
    {
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Observacao).MaximumLength(300);
    }
}

public class RegistrarEntradaEstoqueEpiCommandHandler : IRequestHandler<RegistrarEntradaEstoqueEpiCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarEntradaEstoqueEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarEntradaEstoqueEpiCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoEpis.AnyAsync(c => c.Id == request.CatalogoEpiId, ct))
            throw new KeyNotFoundException($"EPI de catálogo {request.CatalogoEpiId} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var estoque = await _db.EstoquesEpi
            .FirstOrDefaultAsync(x => x.CatalogoEpiId == request.CatalogoEpiId && x.ObraId == request.ObraId, ct);
        if (estoque is null)
        {
            estoque = new EstoqueEpi { CatalogoEpiId = request.CatalogoEpiId, ObraId = request.ObraId, Saldo = 0 };
            _db.EstoquesEpi.Add(estoque);
        }

        estoque.Saldo += request.Quantidade;
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpi.EntradaManual,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            Observacao = request.Observacao,
        });

        await _db.SaveChangesAsync(ct);
    }
}
