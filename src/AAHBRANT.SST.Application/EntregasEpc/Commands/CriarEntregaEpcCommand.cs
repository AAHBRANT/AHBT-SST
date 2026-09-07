using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpc.Commands;

public record CriarEntregaEpcCommand(
    Guid TrabalhadorId,
    Guid CatalogoEpcId,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaEpc MotivoTipo,
    string? Observacoes) : IRequest<Guid>;

public class CriarEntregaEpcCommandValidator : AbstractValidator<CriarEntregaEpcCommand>
{
    public CriarEntregaEpcCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpcId).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.Observacoes).MaximumLength(300);
    }
}

// Mesmo princípio de bloqueio de CriarEntregaUniformeCommand, com uma trava a menos: EPC não tem
// tamanho, então não existe uma "Trava 2" de tamanho cadastrado — só matriz (Trava 1) e estoque
// (Trava 2 aqui), ambas sem válvula de escape.
public class CriarEntregaEpcCommandHandler : IRequestHandler<CriarEntregaEpcCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEntregaEpcCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Trava 1: o item precisa estar na matriz de EPC da função do trabalhador — sem válvula de
        // escape (mesmo princípio de CriarEntregaUniformeCommand/CriarEntregaEpiCommand).
        var itensDaMatriz = await _db.MatrizEpcFuncoes
            .Where(m => m.FuncaoId == trabalhador.FuncaoId)
            .Select(m => m.CatalogoEpcId)
            .ToListAsync(ct);
        if (itensDaMatriz.Count == 0)
            throw new InvalidOperationException("A matriz de EPC da função deste trabalhador ainda não foi cadastrada — cadastre a matriz da função antes de registrar entregas.");
        if (!itensDaMatriz.Contains(request.CatalogoEpcId))
            throw new InvalidOperationException("Este item não faz parte da matriz de EPC da função deste trabalhador — ajuste a matriz da função se este item deveria estar nela.");

        // Trava 2: estoque do item na Obra do trabalhador precisa ter saldo suficiente.
        var estoque = await _db.EstoquesEpc
            .FirstOrDefaultAsync(x => x.CatalogoEpcId == request.CatalogoEpcId && x.ObraId == trabalhador.ObraId, ct);
        var saldoAtual = estoque?.Saldo ?? 0;
        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para este item de EPC nesta obra (saldo atual: {saldoAtual}).");

        var entrega = new EntregaEpc
        {
            TrabalhadorId = request.TrabalhadorId,
            CatalogoEpcId = request.CatalogoEpcId,
            Quantidade = request.Quantidade,
            DataEntrega = request.DataEntrega,
            MotivoTipo = request.MotivoTipo,
            Observacoes = request.Observacoes,
        };
        _db.EntregasEpc.Add(entrega);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
        {
            EstoqueEpcId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpc.SaidaEntrega,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            EntregaEpcId = entrega.Id,
        });

        await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }
}
