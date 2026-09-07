using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesUniforme.Commands;

// Correção manual de estoque (ex.: divergência de inventário) para um tamanho específico numa
// Obra — recebe o saldo final desejado (não um delta); o handler calcula a diferença e registra a
// movimentação com o delta com sinal (pode ser negativo). Observação é obrigatória, mesmo padrão
// de AjustarEstoqueEpiCommand.
public record AjustarEstoqueUniformeCommand(
    Guid CatalogoUniformeId,
    Guid ObraId,
    string Tamanho,
    int NovoSaldo,
    string Observacao) : IRequest;

public class AjustarEstoqueUniformeCommandValidator : AbstractValidator<AjustarEstoqueUniformeCommand>
{
    public AjustarEstoqueUniformeCommandValidator()
    {
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Tamanho).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NovoSaldo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Observacao).NotEmpty().MaximumLength(300);
    }
}

public class AjustarEstoqueUniformeCommandHandler : IRequestHandler<AjustarEstoqueUniformeCommand>
{
    private readonly IAppDbContext _db;
    public AjustarEstoqueUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AjustarEstoqueUniformeCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoUniformes.AnyAsync(c => c.Id == request.CatalogoUniformeId, ct))
            throw new KeyNotFoundException($"Peça de uniforme {request.CatalogoUniformeId} não encontrada.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == request.ObraId
                && x.Tamanho == request.Tamanho, ct);
        if (estoque is null)
        {
            estoque = new EstoqueUniforme
            {
                CatalogoUniformeId = request.CatalogoUniformeId,
                ObraId = request.ObraId,
                Tamanho = request.Tamanho,
                Saldo = 0,
            };
            _db.EstoquesUniforme.Add(estoque);
        }

        var delta = request.NovoSaldo - estoque.Saldo;
        if (delta != 0)
        {
            estoque.Saldo = request.NovoSaldo;
            _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
            {
                EstoqueUniformeId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueUniforme.AjusteManual,
                Quantidade = delta,
                SaldoResultante = estoque.Saldo,
                Observacao = request.Observacao,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
