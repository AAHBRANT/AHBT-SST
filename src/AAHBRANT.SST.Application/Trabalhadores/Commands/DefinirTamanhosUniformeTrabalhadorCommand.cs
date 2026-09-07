using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record ItemTamanhoUniforme(Guid CatalogoUniformeId, string Tamanho);

public record DefinirTamanhosUniformeTrabalhadorCommand(Guid TrabalhadorId, List<ItemTamanhoUniforme> Itens) : IRequest;

public class DefinirTamanhosUniformeTrabalhadorCommandValidator : AbstractValidator<DefinirTamanhosUniformeTrabalhadorCommand>
{
    public DefinirTamanhosUniformeTrabalhadorCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.CatalogoUniformeId).NotEmpty();
            item.RuleFor(i => i.Tamanho).NotEmpty().MaximumLength(20);
        });
    }
}

public class DefinirTamanhosUniformeTrabalhadorCommandHandler : IRequestHandler<DefinirTamanhosUniformeTrabalhadorCommand>
{
    private readonly IAppDbContext _db;
    public DefinirTamanhosUniformeTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirTamanhosUniformeTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhadorExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct);
        if (!trabalhadorExiste)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        // Mesmo padrão de upsert-com-desativação de DefinirMatrizUniformeFuncaoCommand, com uma
        // diferença: aqui cada vínculo também carrega um valor (Tamanho) que pode mudar entre
        // duas chamadas, não só um vínculo booleano — reenviar a mesma peça com tamanho diferente
        // atualiza o tamanho do vínculo existente em vez de criar um novo.
        var vinculosAtuais = await _db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters()
            .Where(t => t.TrabalhadorId == request.TrabalhadorId)
            .ToListAsync(ct);

        var desejados = request.Itens.ToDictionary(i => i.CatalogoUniformeId, i => i.Tamanho);

        foreach (var vinculo in vinculosAtuais)
        {
            if (desejados.TryGetValue(vinculo.CatalogoUniformeId, out var tamanho))
            {
                vinculo.Ativo = true;
                vinculo.Tamanho = tamanho;
            }
            else
            {
                vinculo.Ativo = false;
            }
        }

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoUniformeId).ToHashSet();
        foreach (var item in request.Itens.Where(i => !idsExistentes.Contains(i.CatalogoUniformeId)))
        {
            _db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme
            {
                TrabalhadorId = request.TrabalhadorId,
                CatalogoUniformeId = item.CatalogoUniformeId,
                Tamanho = item.Tamanho,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
