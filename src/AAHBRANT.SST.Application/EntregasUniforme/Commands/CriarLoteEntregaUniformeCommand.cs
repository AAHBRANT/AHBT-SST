using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AAHBRANT.SST.Application.EntregasUniforme.Commands;

public record ItemEntregaUniforme(Guid CatalogoUniformeId, string Tamanho, int Quantidade);
public record CriarLoteEntregaUniformeCommand(Guid TrabalhadorId, DateTime DataEntrega,
    MotivoEntregaUniforme MotivoTipo, string? Observacoes, List<ItemEntregaUniforme> Itens, Guid? OperacaoId = null) : IRequest<List<Guid>>;

public class CriarLoteEntregaUniformeCommandValidator : AbstractValidator<CriarLoteEntregaUniformeCommand>
{
    public CriarLoteEntregaUniformeCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.Observacoes).MaximumLength(300);
        RuleFor(x => x.OperacaoId).Must(id => id is null || id != Guid.Empty);
        RuleFor(x => x.Itens).NotNull().NotEmpty().Must(x => x is null || x.Count <= 50);
        RuleFor(x => x.Itens).Must(x => x is null || x.All(i => i is not null) && x.Select(i => i.CatalogoUniformeId).Distinct().Count() == x.Count)
            .WithMessage("Cada peça deve aparecer uma única vez no kit.");
        RuleForEach(x => x.Itens).ChildRules(item => {
            item.RuleFor(x => x.CatalogoUniformeId).NotEmpty();
            item.RuleFor(x => x.Tamanho).NotEmpty().MaximumLength(20);
            item.RuleFor(x => x.Quantidade).GreaterThan(0);
        });
    }
}

public class CriarLoteEntregaUniformeCommandHandler(IAppDbContext db)
    : IRequestHandler<CriarLoteEntregaUniformeCommand, List<Guid>>
{
    public async Task<List<Guid>> Handle(CriarLoteEntregaUniformeCommand request, CancellationToken ct)
    {
        // Também valida quando usado fora do pipeline (testes/integrações internas).
        await new CriarLoteEntregaUniformeCommandValidator().ValidateAndThrowAsync(request, ct);
        List<Guid>? estaveis = request.OperacaoId is null ? null : request.Itens.Select((_, i) =>
            new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"uniforme:{request.TrabalhadorId}:{request.OperacaoId}:{i}"))[..16])).ToList();
        if (estaveis is not null)
        {
            // Verifica escopo antes do replay. IDs estáveis impedem uma segunda baixa mesmo se
            // a resposta se perder antes de o middleware gravar seu cache de idempotência.
            if (!await db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
                throw new KeyNotFoundException("Trabalhador não encontrado.");
            var existentes = await db.EntregasUniforme.Where(e => estaveis.Contains(e.Id)).ToListAsync(ct);
            if (existentes.Count > 0)
            {
                if (existentes.Count != estaveis.Count || request.Itens.Where((item, i) => !existentes.Any(e =>
                    e.Id == estaveis[i] && e.TrabalhadorId == request.TrabalhadorId && e.CatalogoUniformeId == item.CatalogoUniformeId
                    && e.Tamanho == item.Tamanho.Trim().ToUpperInvariant() && e.Quantidade == item.Quantidade
                    && e.DataEntrega == request.DataEntrega && e.MotivoTipo == request.MotivoTipo && e.Observacoes == request.Observacoes)).Any())
                    throw new InvalidOperationException("Esta operação já foi usada para outro kit. Confira as entregas registradas.");
                return estaveis;
            }
        }
        var criador = new CriarEntregaUniformeCommandHandler(db);
        var ids = new List<Guid>();
        foreach (var (item, i) in request.Itens.Select((item, i) => (item, i)))
            ids.Add(await criador.PrepararAsync(new(request.TrabalhadorId, item.CatalogoUniformeId,
                item.Quantidade, request.DataEntrega, request.MotivoTipo, request.Observacoes, item.Tamanho), ct, salvar: false, entregaId: estaveis?[i]));
        // Uma única SaveChanges é transacional no SQL Server. Qualquer falta de saldo ou conflito
        // de RowVersion impede todas as baixas; não há kit parcialmente persistido.
        await db.SaveChangesAsync(ct);
        return ids;
    }
}
