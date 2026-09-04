using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpc.Commands;

public record RegistrarEntradaEstoqueEpcCommand(
    Guid CatalogoEpcId,
    Guid ObraId,
    int Quantidade,
    string? Observacao) : IRequest;

public class RegistrarEntradaEstoqueEpcCommandValidator : AbstractValidator<RegistrarEntradaEstoqueEpcCommand>
{
    public RegistrarEntradaEstoqueEpcCommandValidator()
    {
        RuleFor(x => x.CatalogoEpcId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Observacao).MaximumLength(300);
    }
}

public class RegistrarEntradaEstoqueEpcCommandHandler : IRequestHandler<RegistrarEntradaEstoqueEpcCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarEntradaEstoqueEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarEntradaEstoqueEpcCommand request, CancellationToken ct)
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

        estoque.Saldo += request.Quantidade;
        _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
        {
            EstoqueEpcId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpc.EntradaManual,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            Observacao = request.Observacao,
        });

        await _db.SaveChangesAsync(ct);
    }
}
