using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.ChecklistModelos.Commands;

// §24 "O sistema deverá permitir diferentes versões do checklist" — implementado como cadeia
// append-only: a versão anterior é desativada (Ativo=false) e uma nova linha nasce apontando
// para ela via ChecklistModeloAnteriorId, preservando o histórico em vez de sobrescrever.
public record CriarNovaVersaoChecklistModeloCommand(
    Guid ChecklistModeloId,
    List<CriarChecklistModeloItemInput> Itens) : IRequest<Guid>;

public class CriarNovaVersaoChecklistModeloCommandValidator : AbstractValidator<CriarNovaVersaoChecklistModeloCommand>
{
    public CriarNovaVersaoChecklistModeloCommandValidator()
    {
        RuleFor(x => x.ChecklistModeloId).NotEmpty();
        RuleFor(x => x.Itens).NotEmpty();
    }
}

public class CriarNovaVersaoChecklistModeloCommandHandler : IRequestHandler<CriarNovaVersaoChecklistModeloCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarNovaVersaoChecklistModeloCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarNovaVersaoChecklistModeloCommand request, CancellationToken ct)
    {
        var anterior = await _db.ChecklistModelos.FirstOrDefaultAsync(c => c.Id == request.ChecklistModeloId, ct)
            ?? throw new KeyNotFoundException($"Checklist {request.ChecklistModeloId} não encontrado.");

        var novaVersao = new ChecklistModelo
        {
            Nome = anterior.Nome,
            TipoInspecao = anterior.TipoInspecao,
            Versao = anterior.Versao + 1,
            ChecklistModeloAnteriorId = anterior.Id,
        };

        var ordem = 1;
        foreach (var item in request.Itens)
        {
            novaVersao.Itens.Add(new ChecklistModeloItem
            {
                Ordem = ordem++,
                Descricao = item.Descricao,
                ExigeFotografia = item.ExigeFotografia,
                ExigeResponsavel = item.ExigeResponsavel,
                ExigePrazo = item.ExigePrazo,
            });
        }

        anterior.Ativo = false;

        _db.ChecklistModelos.Add(novaVersao);
        await _db.SaveChangesAsync(ct);
        return novaVersao.Id;
    }
}
