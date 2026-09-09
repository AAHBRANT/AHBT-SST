using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpc.Commands;

public record AjustarEstoqueEpcCommand(
    Guid CatalogoEpcId,
    Guid ObraId,
    int NovoSaldo,
    string Observacao) : IRequest;

public class AjustarEstoqueEpcCommandValidator : AbstractValidator<AjustarEstoqueEpcCommand>
{
    public AjustarEstoqueEpcCommandValidator()
    {
        RuleFor(x => x.CatalogoEpcId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.NovoSaldo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Observacao).NotEmpty().MaximumLength(300);
    }
}

public class AjustarEstoqueEpcCommandHandler : IRequestHandler<AjustarEstoqueEpcCommand>
{
    private readonly IAppDbContext _db;
    public AjustarEstoqueEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AjustarEstoqueEpcCommand request, CancellationToken ct)
    {
        if (!await _db.CatalogoEpcs.AnyAsync(c => c.Id == request.CatalogoEpcId, ct))
            throw new KeyNotFoundException($"EPC de catálogo {request.CatalogoEpcId} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var estoque = await _db.EstoquesEpc
            .FirstOrDefaultAsync(x => x.CatalogoEpcId == request.CatalogoEpcId && x.ObraId == request.ObraId, ct);
        if (estoque is null)
        {
            estoque = new EstoqueEpc { CatalogoEpcId = request.CatalogoEpcId, ObraId = request.ObraId, Saldo = 0 };
            _db.EstoquesEpc.Add(estoque);
        }

        var delta = request.NovoSaldo - estoque.Saldo;
        if (delta != 0)
        {
            estoque.Saldo = request.NovoSaldo;
            _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
            {
                EstoqueEpcId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueEpc.AjusteManual,
                Quantidade = delta,
                SaldoResultante = estoque.Saldo,
                Observacao = request.Observacao,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
