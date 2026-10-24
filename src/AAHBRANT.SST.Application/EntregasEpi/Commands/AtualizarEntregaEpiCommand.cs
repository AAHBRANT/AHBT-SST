using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EstoquesEpi.Minimo;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record AtualizarEntregaEpiCommand(
    Guid Id,
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    int Quantidade,
    int? QuantidadeDevolucao,
    string? VistoConsorcioResponsavel,
    string? Motivo,
    string? Observacoes,
    MotivoEntregaEpi MotivoTipo,
    string? NumeroListaPresencaNr6,
    DateTime? DataTreinamentoNr6) : IRequest;

public class AtualizarEntregaEpiCommandValidator : AbstractValidator<AtualizarEntregaEpiCommand>
{
    public AtualizarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.NumeroListaPresencaNr6).MaximumLength(50);
    }
}

public class AtualizarEntregaEpiCommandHandler : IRequestHandler<AtualizarEntregaEpiCommand>
{
    private readonly IAppDbContext _db;
    private readonly IVerificacaoEstoqueMinimoService? _verificacaoMinimo;

    public AtualizarEntregaEpiCommandHandler(IAppDbContext db, IVerificacaoEstoqueMinimoService? verificacaoMinimo = null)
    {
        _db = db;
        _verificacaoMinimo = verificacaoMinimo;
    }

    public async Task Handle(AtualizarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.NoEscopoDoTrabalhador(_db, x => x.TrabalhadorId).FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        // Entrega assinada (12/10): os dados da entrega ficam travados, mas a devolução continua podendo ser
        // registrada — é por este mesmo comando que a tela registra a devolução, quase sempre depois de a
        // entrega ter sido assinada. Depois que a devolução é assinada, trava tudo.
        const ProtecaoDocumentoAssinado.Trava naAssinatura = ProtecaoDocumentoAssinado.Trava.AposPrimeiraAssinatura;
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, request.Id, naAssinatura, ct, "DevolucaoEpi");
        if (await ProtecaoDocumentoAssinado.EstaTravadoAsync(_db, request.Id, naAssinatura, ct, "EntregaEpi") && MudouDadosDaEntrega(entrega, request))
            throw new InvalidOperationException("Esta entrega já foi assinada: só é possível registrar a devolução. Para corrigir a entrega, registre uma nova.");

        var trabalhadorOriginal = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == entrega.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        var obraEstoque = trabalhadorOriginal.ObraId;

        if (request.TrabalhadorId != entrega.TrabalhadorId)
            await _db.GarantirTrabalhadorNoEscopoAsync(request.TrabalhadorId, ct);

        // Correção da entrega (teste E2E 21/10): editar quantidade, EPI ou trabalhador não mexia no
        // estoque, e a exclusão depois estornava os valores já editados — 1→2 e excluir deixava uma
        // unidade a mais no saldo; trocar o EPI deixava um item a menos e outro a mais. Agora a
        // correção acerta o estoque na hora, com as mesmas travas da criação (saldo suficiente).
        var novoTrabalhador = request.TrabalhadorId == entrega.TrabalhadorId
            ? trabalhadorOriginal
            : await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
                ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        var mudouBaixaDeEstoque = request.CatalogoEpiId != entrega.CatalogoEpiId
            || request.Quantidade != entrega.Quantidade
            || novoTrabalhador.ObraId != trabalhadorOriginal.ObraId;
        if (mudouBaixaDeEstoque)
        {
            // Com devolução já registrada, parte do item já voltou ao saldo pela quantidade antiga —
            // recalcular por cima disso é onde o estoque some ou aparece do nada.
            if (entrega.DataDevolucao is not null)
                throw new InvalidOperationException("Esta entrega já tem devolução registrada: não é possível alterar EPI, quantidade ou trabalhador. Para corrigir, registre uma nova entrega.");

            await AcertarEstoqueDaCorrecaoAsync(entrega, trabalhadorOriginal.ObraId, request.CatalogoEpiId, novoTrabalhador.ObraId, request.Quantidade, ct);
            obraEstoque = novoTrabalhador.ObraId;
        }

        // Devolução registrada agora (não tinha DataDevolucao antes): repõe o estoque segmentado por
        // Obra (Fase 3) com a quantidade devolvida, no estoque de onde o item saiu (o já corrigido acima,
        // se a mesma requisição também corrigiu a entrega).
        if (entrega.DataDevolucao is null && request.DataDevolucao is not null)
        {
            var estoque = await _db.EstoquesEpi
                .FirstOrDefaultAsync(x => x.CatalogoEpiId == request.CatalogoEpiId && x.ObraId == obraEstoque, ct);
            if (estoque is null)
            {
                estoque = new EstoqueEpi { CatalogoEpiId = request.CatalogoEpiId, ObraId = obraEstoque, Saldo = 0 };
                _db.EstoquesEpi.Add(estoque);
            }

            var quantidadeDevolvida = request.QuantidadeDevolucao ?? request.Quantidade;
            estoque.Saldo += quantidadeDevolvida;
            _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
            {
                EstoqueEpiId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueEpi.DevolucaoEntrada,
                Quantidade = quantidadeDevolvida,
                SaldoResultante = estoque.Saldo,
                EntregaEpiId = entrega.Id,
            });
        }

        entrega.TrabalhadorId = request.TrabalhadorId;
        entrega.CatalogoEpiId = request.CatalogoEpiId;
        entrega.DataEntrega = request.DataEntrega;
        entrega.DataDevolucao = request.DataDevolucao;
        entrega.DataValidade = request.DataValidade;
        entrega.Quantidade = request.Quantidade;
        entrega.QuantidadeDevolucao = request.QuantidadeDevolucao;
        entrega.VistoConsorcioResponsavel = request.VistoConsorcioResponsavel;
        entrega.Motivo = request.Motivo;
        entrega.Observacoes = request.Observacoes;
        entrega.MotivoTipo = request.MotivoTipo;
        entrega.NumeroListaPresencaNr6 = request.NumeroListaPresencaNr6;
        entrega.DataTreinamentoNr6 = request.DataTreinamentoNr6;

        await _db.SaveChangesAsync(ct);

        // Alerta de estoque mínimo da obra (especificação 21/10/2026, Módulo 2).
        if (_verificacaoMinimo is not null) await _verificacaoMinimo.VerificarAsync(obraEstoque, ct);
        if (_verificacaoMinimo is not null && obraEstoque != trabalhadorOriginal.ObraId)
            await _verificacaoMinimo.VerificarAsync(trabalhadorOriginal.ObraId, ct);
    }

    // Mesmo EPI na mesma obra: movimenta só a diferença. EPI ou obra diferente: estorna tudo no estoque
    // antigo e dá a baixa inteira no novo. Saldo insuficiente bloqueia, como na criação da entrega.
    private async Task AcertarEstoqueDaCorrecaoAsync(
        EntregaEpi entrega, Guid obraAntiga, Guid catalogoNovo, Guid obraNova, int quantidadeNova, CancellationToken ct)
    {
        var mesmoEstoque = catalogoNovo == entrega.CatalogoEpiId && obraNova == obraAntiga;
        var devolverAoAntigo = mesmoEstoque ? Math.Max(0, entrega.Quantidade - quantidadeNova) : entrega.Quantidade;
        var baixarDoNovo = mesmoEstoque ? Math.Max(0, quantidadeNova - entrega.Quantidade) : quantidadeNova;

        if (baixarDoNovo > 0)
        {
            var estoqueNovo = await _db.EstoquesEpi
                .FirstOrDefaultAsync(x => x.CatalogoEpiId == catalogoNovo && x.ObraId == obraNova, ct);
            var saldoAtual = estoqueNovo?.Saldo ?? 0;
            if (saldoAtual < baixarDoNovo)
                throw new InvalidOperationException($"Estoque insuficiente para este EPI nesta obra (saldo atual: {saldoAtual}).");
            var reservadoParaOutros = await SituacaoReservasEpi.ReservadoParaOutrosAsync(_db, obraNova, catalogoNovo, entrega.TrabalhadorId, ct);
            if (saldoAtual - reservadoParaOutros < baixarDoNovo)
                throw new InvalidOperationException(SituacaoReservasEpi.MensagemSaldoComprometido(saldoAtual, reservadoParaOutros));

            estoqueNovo!.Saldo -= baixarDoNovo;
            _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
            {
                EstoqueEpiId = estoqueNovo.Id,
                Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega,
                Quantidade = baixarDoNovo,
                SaldoResultante = estoqueNovo.Saldo,
                EntregaEpiId = entrega.Id,
                Observacao = "Correção de entrega já registrada.",
            });
        }

        if (devolverAoAntigo > 0)
        {
            var estoqueAntigo = await _db.EstoquesEpi
                .FirstOrDefaultAsync(x => x.CatalogoEpiId == entrega.CatalogoEpiId && x.ObraId == obraAntiga, ct);
            if (estoqueAntigo is null)
            {
                estoqueAntigo = new EstoqueEpi { CatalogoEpiId = entrega.CatalogoEpiId, ObraId = obraAntiga, Saldo = 0 };
                _db.EstoquesEpi.Add(estoqueAntigo);
            }

            estoqueAntigo.Saldo += devolverAoAntigo;
            _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
            {
                EstoqueEpiId = estoqueAntigo.Id,
                Tipo = TipoMovimentacaoEstoqueEpi.AjusteManual,
                Quantidade = devolverAoAntigo,
                SaldoResultante = estoqueAntigo.Saldo,
                EntregaEpiId = entrega.Id,
                Observacao = $"Estorno automático da correção de uma entrega registrada em {entrega.DataEntrega:dd/MM/yyyy}.",
            });
        }
    }

    // Tudo menos os campos da devolução. Datas comparadas pelo dia (a tela reenvia a data sem hora); motivo
    // vazio nas entregas antigas é reenviado pela tela como "Inicial" e não conta como alteração.
    private static bool MudouDadosDaEntrega(EntregaEpi entrega, AtualizarEntregaEpiCommand r) =>
        r.TrabalhadorId != entrega.TrabalhadorId
        || r.CatalogoEpiId != entrega.CatalogoEpiId
        || r.DataEntrega.Date != entrega.DataEntrega.Date
        || r.DataValidade?.Date != entrega.DataValidade?.Date
        || r.Quantidade != entrega.Quantidade
        || (r.VistoConsorcioResponsavel ?? "") != (entrega.VistoConsorcioResponsavel ?? "")
        || (r.Motivo ?? "") != (entrega.Motivo ?? "")
        || (r.Observacoes ?? "") != (entrega.Observacoes ?? "")
        || r.MotivoTipo != (entrega.MotivoTipo ?? MotivoEntregaEpi.Inicial)
        || (r.NumeroListaPresencaNr6 ?? "") != (entrega.NumeroListaPresencaNr6 ?? "")
        || r.DataTreinamentoNr6?.Date != entrega.DataTreinamentoNr6?.Date;
}
