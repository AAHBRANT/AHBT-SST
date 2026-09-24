using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Commands;

public record ExcluirInstalacaoEpcCommand(Guid Id) : IRequest;

public class ExcluirInstalacaoEpcCommandValidator : AbstractValidator<ExcluirInstalacaoEpcCommand>
{
    public ExcluirInstalacaoEpcCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirInstalacaoEpcCommandHandler : IRequestHandler<ExcluirInstalacaoEpcCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirInstalacaoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirInstalacaoEpcCommand request, CancellationToken ct)
    {
        var instalacao = await _db.InstalacoesEpc.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Instalação de EPC não encontrada.");

        await EstornarEstoqueAsync(instalacao, ct);

        _db.InstalacoesEpc.Remove(instalacao);
        await _db.SaveChangesAsync(ct);
    }

    // Mesmo defeito que a entrega de EPI tinha (corrigido em 24/09): instalar baixa o estoque da
    // obra (CriarInstalacaoEpcCommand) e excluir não devolvia nada — cada instalação apagada tirava
    // unidades do saldo em definitivo, sem rastro. Passou a doer quando o Administrador ganhou
    // botão de excluir na tela para limpar lançamento de teste.
    //
    // Instalação já removida (DataRemocao preenchida) não estorna: o saldo dela já voltou em
    // RegistrarRemocaoEpcCommand, e somar de novo criaria estoque do nada.
    private async Task EstornarEstoqueAsync(InstalacaoEpc instalacao, CancellationToken ct)
    {
        if (instalacao.DataRemocao is not null) return;

        var estoque = await _db.EstoquesEpc
            .FirstOrDefaultAsync(e => e.CatalogoEpcId == instalacao.CatalogoEpcId && e.ObraId == instalacao.ObraId, ct);
        if (estoque is null) return;

        estoque.Saldo += instalacao.Quantidade;
        _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
        {
            EstoqueEpcId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpc.AjusteManual,
            Quantidade = instalacao.Quantidade,
            SaldoResultante = estoque.Saldo,
            // Sem InstalacaoEpcId: a instalação é apagada nesta mesma operação.
            Observacao = $"Estorno automático da exclusão de uma instalação de {instalacao.Quantidade} unidade(s) registrada em {instalacao.DataInstalacao:dd/MM/yyyy}.",
        });
    }
}
