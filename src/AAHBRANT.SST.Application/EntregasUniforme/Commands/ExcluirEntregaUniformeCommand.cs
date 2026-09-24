using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasUniforme.Commands;

// Entrega de uniforme era o único dos três controles de material (EPI, EPC, Uniforme) sem exclusão
// nenhuma: nem comando, nem endpoint. Criado em 24/09 pelo mesmo motivo dos outros — o usuário
// precisa tirar da lista o que lançou testando, antes de liberar o sistema para a equipe.
public record ExcluirEntregaUniformeCommand(Guid Id) : IRequest;

public class ExcluirEntregaUniformeCommandValidator : AbstractValidator<ExcluirEntregaUniformeCommand>
{
    public ExcluirEntregaUniformeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirEntregaUniformeCommandHandler : IRequestHandler<ExcluirEntregaUniformeCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirEntregaUniformeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirEntregaUniformeCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasUniforme.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de uniforme não encontrada.");

        await EstornarEstoqueAsync(entrega, ct);

        _db.EntregasUniforme.Remove(entrega);
        await _db.SaveChangesAsync(ct);
    }

    // Registrar a entrega debita o saldo do bucket peça+tamanho na obra (CriarEntregaUniformeCommand);
    // excluir devolve a mesma quantidade. Diferente do EPI, uniforme não tem devolução parcial — a
    // entidade não guarda quantidade devolvida —, então volta tudo.
    //
    // O bucket é resolvido por peça + tamanho + obra do trabalhador, igual à baixa: o mesmo uniforme
    // em tamanho diferente é outro saldo, e devolver no bucket errado estragaria os dois.
    private async Task EstornarEstoqueAsync(EntregaUniforme entrega, CancellationToken ct)
    {
        if (entrega.Quantidade <= 0) return;

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == entrega.TrabalhadorId, ct);
        if (trabalhador is null) return;

        var estoque = await _db.EstoquesUniforme.FirstOrDefaultAsync(e => e.CatalogoUniformeId == entrega.CatalogoUniformeId
            && e.ObraId == trabalhador.ObraId
            && e.Tamanho == entrega.Tamanho, ct);
        if (estoque is null) return;

        estoque.Saldo += entrega.Quantidade;
        _db.MovimentacoesEstoqueUniforme.Add(new MovimentacaoEstoqueUniforme
        {
            EstoqueUniformeId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueUniforme.AjusteManual,
            Quantidade = entrega.Quantidade,
            SaldoResultante = estoque.Saldo,
            // Sem EntregaUniformeId: a entrega é apagada nesta mesma operação.
            Observacao = $"Estorno automático da exclusão de uma entrega de {entrega.Quantidade} unidade(s) (tamanho {entrega.Tamanho}) registrada em {entrega.DataEntrega:dd/MM/yyyy}.",
        });
    }
}
