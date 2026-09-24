using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record ExcluirEntregaEpiCommand(Guid Id) : IRequest;

public class ExcluirEntregaEpiCommandValidator : AbstractValidator<ExcluirEntregaEpiCommand>
{
    public ExcluirEntregaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirEntregaEpiCommandHandler : IRequestHandler<ExcluirEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        await EstornarEstoqueAsync(entrega, ct);

        _db.EntregasEpi.Remove(entrega);
        await _db.SaveChangesAsync(ct);
    }

    // Registrar a entrega baixa o estoque da obra (CriarEntregaEpiCommand), mas excluir não devolvia
    // nada: cada entrega apagada deixava o saldo permanentemente menor, sem rastro. Passou a doer
    // agora que o Administrador tem botão de excluir na tela (23/09) e apaga lançamento de teste —
    // dez testes apagados, dez unidades sumidas do estoque do canteiro.
    //
    // Devolve só o que ainda está com o trabalhador: o que já tinha sido devolvido antes já voltou
    // ao saldo pela devolução (AtualizarEntregaEpiCommand), e estornar de novo criaria estoque do
    // nada. A movimentação fica registrada como AjusteManual — o histórico do EPI precisa explicar
    // de onde veio o saldo, ainda mais quando a entrega que o originou não existe mais.
    private async Task EstornarEstoqueAsync(EntregaEpi entrega, CancellationToken ct)
    {
        var quantidadeEmPosse = entrega.Quantidade - (entrega.QuantidadeDevolucao ?? 0);
        if (quantidadeEmPosse <= 0) return;

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == entrega.TrabalhadorId, ct);
        if (trabalhador is null) return;

        var estoque = await _db.EstoquesEpi
            .FirstOrDefaultAsync(e => e.CatalogoEpiId == entrega.CatalogoEpiId && e.ObraId == trabalhador.ObraId, ct);
        if (estoque is null) return;

        estoque.Saldo += quantidadeEmPosse;
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpi.AjusteManual,
            Quantidade = quantidadeEmPosse,
            SaldoResultante = estoque.Saldo,
            // Sem EntregaEpiId: a entrega é apagada nesta mesma operação e a coluna ficaria órfã.
            Observacao = $"Estorno automático da exclusão de uma entrega de {quantidadeEmPosse} unidade(s) registrada em {entrega.DataEntrega:dd/MM/yyyy}.",
        });
    }
}
