using AAHBRANT.SST.Application.Common.Interfaces;
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

        // Devolução registrada agora (não tinha DataDevolucao antes): repõe o estoque do catálogo
        // com a quantidade devolvida, para manter SaldoEstoque coerente com o item físico voltando.
        if (entrega.DataDevolucao is null && request.DataDevolucao is not null)
        {
            var catalogo = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == entrega.CatalogoEpiId, ct)
                ?? throw new KeyNotFoundException("EPI de catálogo não encontrado.");
            catalogo.SaldoEstoque += request.QuantidadeDevolucao ?? entrega.Quantidade;
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
