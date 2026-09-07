using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Commands;

// Entrada manual de estoque (compra/reposição) numa Obra, para um tamanho específico da peça —
// cria a linha de EstoqueUniforme se ainda não existir (primeiro tamanho desse item nessa Obra).
public record RegistrarEntradaEstoqueUniformeCommand(
    Guid CatalogoUniformeId,
    Guid ObraId,
    string Tamanho,
    int Quantidade,
    string? Observacao) : IRequest;

public class RegistrarEntradaEstoqueUniformeCommandValidator : AbstractValidator<RegistrarEntradaEstoqueUniformeCommand>
{
    public RegistrarEntradaEstoqueUniformeCommandValidator()
    {
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Tamanho).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Observacao).MaximumLength(300);
    }
}

public class RegistrarEntradaEstoqueUniformeCommandHandler : IRequestHandler<RegistrarEntradaEstoqueUniformeCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarEntradaEstoqueUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarEntradaEstoqueUniformeCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoUniformes.AnyAsync(c => c.Id == request.CatalogoUniformeId, ct))
            throw new KeyNotFoundException($"Peça de uniforme {request.CatalogoUniformeId} não encontrada.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var tamanho = request.Tamanho.Trim().ToUpperInvariant();

        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == request.ObraId
                && x.Tamanho == tamanho, ct);
        if (estoque is null)
        {
            estoque = new EstoqueUniforme
            {
                CatalogoUniformeId = request.CatalogoUniformeId,
                ObraId = request.ObraId,
                Tamanho = tamanho,
                Saldo = 0,
            };
            _db.EstoquesUniforme.Add(estoque);
        }

        estoque.Saldo += request.Quantidade;
        _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
        {
            EstoqueUniformeId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueUniforme.EntradaManual,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            Observacao = request.Observacao,
        });

        await _db.SaveChangesAsync(ct);
    }
}
