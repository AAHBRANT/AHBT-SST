using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Commands;

public record CriarEntregaUniformeCommand(
    Guid TrabalhadorId,
    Guid CatalogoUniformeId,
    int Quantidade,
    DateTime DataEntrega,
    MotivoEntregaUniforme MotivoTipo,
    string? Observacoes,
    string? Tamanho = null) : IRequest<Guid>;

public class CriarEntregaUniformeCommandValidator : AbstractValidator<CriarEntregaUniformeCommand>
{
    public CriarEntregaUniformeCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoUniformeId).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.Observacoes).MaximumLength(300);
        RuleFor(x => x.Tamanho).MaximumLength(20).Must(t => t is null || !string.IsNullOrWhiteSpace(t));
    }
}

public class CriarEntregaUniformeCommandHandler : IRequestHandler<CriarEntregaUniformeCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaUniformeCommandHandler(IAppDbContext db) => _db = db;

    public Task<Guid> Handle(CriarEntregaUniformeCommand request, CancellationToken ct) => PrepararAsync(request, ct, salvar: true);

    internal async Task<Guid> PrepararAsync(CriarEntregaUniformeCommand request, CancellationToken ct, bool salvar, Guid? entregaId = null)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Trava 1: a peça precisa estar na matriz de uniforme da função do trabalhador — sem
        // válvula de escape (mesmo princípio de CriarEntregaEpiCommand para CA vencido/estoque).
        var itensDaMatriz = await _db.MatrizUniformeFuncoes
            .Where(m => m.FuncaoId == trabalhador.FuncaoId)
            .Select(m => m.CatalogoUniformeId)
            .ToListAsync(ct);
        if (itensDaMatriz.Count == 0)
            throw new InvalidOperationException("A matriz de uniforme da função deste trabalhador ainda não foi cadastrada — cadastre a matriz da função antes de registrar entregas.");
        if (!itensDaMatriz.Contains(request.CatalogoUniformeId))
            throw new InvalidOperationException("Esta peça não faz parte da matriz de uniforme da função deste trabalhador — ajuste a matriz da função se esta peça deveria estar nela.");

        // O tamanho escolhido vale só para esta entrega. Clientes antigos continuam usando
        // o tamanho do perfil; os históricos e as preferências do trabalhador não são alterados.
        var tamanho = request.Tamanho?.Trim().ToUpperInvariant() ?? await _db.TrabalhadorTamanhosUniforme
            .Where(t => t.TrabalhadorId == request.TrabalhadorId && t.CatalogoUniformeId == request.CatalogoUniformeId)
            .Select(t => t.Tamanho)
            .FirstOrDefaultAsync(ct);
        if (tamanho is null)
            throw new InvalidOperationException("Este trabalhador não tem um tamanho cadastrado para esta peça — cadastre o tamanho antes de registrar a entrega.");

        // Trava 3: estoque do bucket (peça + tamanho) na Obra do trabalhador precisa ter saldo
        // suficiente — mesmo princípio de bloqueio de estoque insuficiente do EPI.
        var estoque = await _db.EstoquesUniforme
            .FirstOrDefaultAsync(x => x.CatalogoUniformeId == request.CatalogoUniformeId
                && x.ObraId == trabalhador.ObraId
                && x.Tamanho == tamanho, ct);
        var saldoAtual = estoque?.Saldo ?? 0;
        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para esta peça no tamanho {tamanho} nesta obra (saldo atual: {saldoAtual}).");

        var entrega = new EntregaUniforme
        {
            Id = entregaId ?? Guid.NewGuid(),
            TrabalhadorId = request.TrabalhadorId,
            CatalogoUniformeId = request.CatalogoUniformeId,
            Tamanho = tamanho,
            Quantidade = request.Quantidade,
            DataEntrega = request.DataEntrega,
            MotivoTipo = request.MotivoTipo,
            Observacoes = request.Observacoes,
        };
        _db.EntregasUniforme.Add(entrega);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
        {
            EstoqueUniformeId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueUniforme.SaidaEntrega,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            EntregaUniformeId = entrega.Id,
        });

        if (salvar) await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }
}
