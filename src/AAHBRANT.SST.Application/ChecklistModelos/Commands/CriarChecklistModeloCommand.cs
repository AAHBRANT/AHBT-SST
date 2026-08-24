using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.ChecklistModelos.Commands;

public record CriarChecklistModeloItemInput(string Descricao, bool ExigeFotografia, bool ExigeResponsavel, bool ExigePrazo);

// Todo checklist nasce na versão 1 (§24 "diferentes versões" é tratado por
// CriarNovaVersaoChecklistModeloCommand, que encadeia a partir desta).
public record CriarChecklistModeloCommand(
    string Nome,
    TipoInspecao TipoInspecao,
    List<CriarChecklistModeloItemInput> Itens) : IRequest<Guid>;

public class CriarChecklistModeloCommandValidator : AbstractValidator<CriarChecklistModeloCommand>
{
    public CriarChecklistModeloCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty().MaximumLength(500);
        });
    }
}

public class CriarChecklistModeloCommandHandler : IRequestHandler<CriarChecklistModeloCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarChecklistModeloCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarChecklistModeloCommand request, CancellationToken ct)
    {
        var checklist = new ChecklistModelo
        {
            Nome = request.Nome,
            TipoInspecao = request.TipoInspecao,
            Versao = 1,
        };

        var ordem = 1;
        foreach (var item in request.Itens)
        {
            checklist.Itens.Add(new ChecklistModeloItem
            {
                Ordem = ordem++,
                Descricao = item.Descricao,
                ExigeFotografia = item.ExigeFotografia,
                ExigeResponsavel = item.ExigeResponsavel,
                ExigePrazo = item.ExigePrazo,
            });
        }

        _db.ChecklistModelos.Add(checklist);
        await _db.SaveChangesAsync(ct);
        return checklist.Id;
    }
}
