using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record CriarEntregaEpiCommand(
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    int Quantidade,
    string? VistoConsorcioResponsavel,
    string? Motivo,
    string? Observacoes,
    MotivoEntregaEpi MotivoTipo,
    string? NumeroListaPresencaNr6,
    DateTime? DataTreinamentoNr6) : IRequest<Guid>;

public class CriarEntregaEpiCommandValidator : AbstractValidator<CriarEntregaEpiCommand>
{
    public CriarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.NumeroListaPresencaNr6).MaximumLength(50);
    }
}

public class CriarEntregaEpiCommandHandler : IRequestHandler<CriarEntregaEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEntregaEpiCommand request, CancellationToken ct)
    {
        var catalogo = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpiId, ct)
            ?? throw new KeyNotFoundException("EPI de catálogo não encontrado.");

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Bloqueio de entrega com CA vencido e de estoque insuficiente: decisões confirmadas com o
        // usuário — não apenas um aviso, a entrega não é registrada.
        if (catalogo.CertificadoAprovacaoValidade is not null && catalogo.CertificadoAprovacaoValidade < DateTime.UtcNow)
            throw new InvalidOperationException("Este EPI está com o Certificado de Aprovação (CA) vencido — não é possível registrar a entrega.");

        // Fase 3 — estoque segmentado por Obra: resolve a Obra do trabalhador e busca (ou cria, com
        // saldo zero) a linha de estoque desse EPI nessa Obra.
        var estoque = await _db.EstoquesEpi
            .FirstOrDefaultAsync(x => x.CatalogoEpiId == request.CatalogoEpiId && x.ObraId == trabalhador.ObraId, ct);
        var saldoAtual = estoque?.Saldo ?? 0;

        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para este EPI nesta obra (saldo atual: {saldoAtual}).");

        var entrega = new EntregaEpi
        {
            TrabalhadorId = request.TrabalhadorId,
            CatalogoEpiId = request.CatalogoEpiId,
            DataEntrega = request.DataEntrega,
            DataDevolucao = request.DataDevolucao,
            DataValidade = request.DataValidade,
            Quantidade = request.Quantidade,
            VistoConsorcioResponsavel = request.VistoConsorcioResponsavel,
            Motivo = request.Motivo,
            Observacoes = request.Observacoes,
            MotivoTipo = request.MotivoTipo,
            NumeroListaPresencaNr6 = request.NumeroListaPresencaNr6,
            DataTreinamentoNr6 = request.DataTreinamentoNr6,
        };
        _db.EntregasEpi.Add(entrega);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            EntregaEpiId = entrega.Id,
        });

        await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }
}
