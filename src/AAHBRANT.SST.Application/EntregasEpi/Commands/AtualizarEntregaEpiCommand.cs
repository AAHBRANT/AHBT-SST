using AAHBRANT.SST.Application.Common.Interfaces;
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
    public AtualizarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarEntregaEpiCommand request, CancellationToken ct)
    {
        var entrega = await _db.EntregasEpi.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Entrega de EPI não encontrada.");

        // Devolução registrada agora (não tinha DataDevolucao antes): repõe o estoque segmentado por
        // Obra (Fase 3) com a quantidade devolvida. Usa a Obra do trabalhador ANTES da atualização
        // abaixo (entrega.TrabalhadorId), já que foi essa Obra que teve o item físico saindo do estoque.
        if (entrega.DataDevolucao is null && request.DataDevolucao is not null)
        {
            var trabalhadorOriginal = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == entrega.TrabalhadorId, ct)
                ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

            var estoque = await _db.EstoquesEpi
                .FirstOrDefaultAsync(x => x.CatalogoEpiId == entrega.CatalogoEpiId && x.ObraId == trabalhadorOriginal.ObraId, ct);
            if (estoque is null)
            {
                estoque = new EstoqueEpi { CatalogoEpiId = entrega.CatalogoEpiId, ObraId = trabalhadorOriginal.ObraId, Saldo = 0 };
                _db.EstoquesEpi.Add(estoque);
            }

            var quantidadeDevolvida = request.QuantidadeDevolucao ?? entrega.Quantidade;
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
    }
}
