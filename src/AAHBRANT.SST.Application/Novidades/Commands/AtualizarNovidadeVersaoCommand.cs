using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Novidades.Commands;

public record AtualizarNovidadeVersaoCommand(
    Guid Id,
    string Titulo,
    string Versao,
    DateTime DataPublicacao,
    List<NovidadeVersaoItemInput> Itens) : IRequest<NovidadeVersaoDto>;

public class AtualizarNovidadeVersaoCommandValidator : AbstractValidator<AtualizarNovidadeVersaoCommand>
{
    public AtualizarNovidadeVersaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Itens).NotEmpty().WithMessage("Cadastre pelo menos um item de novidade.");
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty().MaximumLength(300);
            item.RuleFor(i => i.Antes).MaximumLength(1000);
            item.RuleFor(i => i.Agora).MaximumLength(1000);
        });
    }
}

public class AtualizarNovidadeVersaoCommandHandler : IRequestHandler<AtualizarNovidadeVersaoCommand, NovidadeVersaoDto>
{
    private readonly IAppDbContext _db;

    public AtualizarNovidadeVersaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<NovidadeVersaoDto> Handle(AtualizarNovidadeVersaoCommand request, CancellationToken ct)
    {
        var novidade = await _db.NovidadesVersao
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Novidade {request.Id} não encontrada.");

        novidade.Titulo = request.Titulo.Trim();
        novidade.Versao = request.Versao.Trim();
        novidade.DataPublicacao = request.DataPublicacao;

        // Substitui a lista de itens por completo — mais simples que fazer diff item a item, e
        // não há nada mais dependendo de um NovidadeVersaoItem.Id específico (é sempre exibido
        // dentro do próprio pop-up, nunca referenciado de fora).
        foreach (var itemAntigo in novidade.Itens.ToList())
        {
            _db.NovidadesVersaoItens.Remove(itemAntigo);
        }

        // _db.NovidadesVersaoItens.Add (não novidade.Itens.Add): a NovidadeVersao pai já está
        // rastreada como Unchanged/Modified (não Added), então o EF Core decide o estado do item
        // novo pela heurística de valor da PK — e Id já vem preenchido por AuditableEntity
        // (`= Guid.NewGuid()`), então ele é tratado como Modified em vez de Added, virando um
        // UPDATE que não bate com nenhuma linha (DbUpdateConcurrencyException, "_excluido: true").
        // Add() direto no DbSet sempre marca Added, independente do valor da PK.
        var ordem = 0;
        foreach (var item in request.Itens)
        {
            _db.NovidadesVersaoItens.Add(new NovidadeVersaoItem
            {
                NovidadeVersaoId = novidade.Id,
                Categoria = item.Categoria,
                Descricao = item.Descricao.Trim(),
                Antes = string.IsNullOrWhiteSpace(item.Antes) ? null : item.Antes.Trim(),
                Agora = string.IsNullOrWhiteSpace(item.Agora) ? null : item.Agora.Trim(),
                Ordem = ordem++,
            });
        }

        await _db.SaveChangesAsync(ct);

        return NovidadesMapper.Mapear(novidade);
    }
}
