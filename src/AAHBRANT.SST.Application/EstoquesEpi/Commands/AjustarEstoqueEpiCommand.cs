using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Commands;

// Correção manual de estoque (ex.: divergência de inventário, perda, avaria) numa Obra — Fase 3.
// Recebe o saldo final desejado (não um delta); o handler calcula a diferença e registra a
// movimentação com o delta com sinal (pode ser negativo). Observação é obrigatória aqui — ao
// contrário da entrada manual, um ajuste sempre exige justificativa para fins de auditoria.
public record AjustarEstoqueEpiCommand(
    Guid CatalogoEpiId,
    Guid ObraId,
    int NovoSaldo,
    string Observacao) : IRequest;

public class AjustarEstoqueEpiCommandValidator : AbstractValidator<AjustarEstoqueEpiCommand>
{
    public AjustarEstoqueEpiCommandValidator()
    {
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.NovoSaldo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Observacao).NotEmpty().MaximumLength(300);
    }
}

public class AjustarEstoqueEpiCommandHandler : IRequestHandler<AjustarEstoqueEpiCommand>
{
    private readonly IAppDbContext _db;
    public AjustarEstoqueEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AjustarEstoqueEpiCommand request, CancellationToken ct)
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

        var delta = request.NovoSaldo - estoque.Saldo;
        if (delta != 0)
        {
            estoque.Saldo = request.NovoSaldo;
            _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
            {
                EstoqueEpiId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueEpi.AjusteManual,
                Quantidade = delta,
                SaldoResultante = estoque.Saldo,
                Observacao = request.Observacao,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
