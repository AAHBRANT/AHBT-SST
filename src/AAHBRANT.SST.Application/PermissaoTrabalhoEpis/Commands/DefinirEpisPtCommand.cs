using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoEpis.Commands;

public record EpiInput(ItemEpiPt Item, string? Complemento);

// §5 do formulário, "EPIs aplicáveis" — mesmo princípio de DefinirTiposTrabalhoPtCommand (replace
// completo). Complemento é o texto livre embutido em algumas opções (ex.: "Luvas: ____").
// OutrosEpis é o campo "Outros EPIs: ____" do mesmo bloco, mora no PermissaoTrabalho em vez de
// virar mais uma linha (é texto livre solto, não um item marcável da lista fixa).
public record DefinirEpisPtCommand(Guid PermissaoTrabalhoId, List<EpiInput> Itens, string? OutrosEpis) : IRequest;

public class DefinirEpisPtCommandValidator : AbstractValidator<DefinirEpisPtCommand>
{
    public DefinirEpisPtCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleFor(x => x.OutrosEpis).MaximumLength(300);
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Item).IsInEnum();
            item.RuleFor(i => i.Complemento).MaximumLength(100);
        });
    }
}

public class DefinirEpisPtCommandHandler : IRequestHandler<DefinirEpisPtCommand>
{
    private readonly IAppDbContext _db;

    public DefinirEpisPtCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirEpisPtCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.PermissaoTrabalhoId, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var atuais = await _db.PermissaoTrabalhoEpis
            .Where(e => e.PermissaoTrabalhoId == request.PermissaoTrabalhoId).ToListAsync(ct);
        _db.PermissaoTrabalhoEpis.RemoveRange(atuais);

        foreach (var item in request.Itens.GroupBy(i => i.Item).Select(g => g.First()))
        {
            _db.PermissaoTrabalhoEpis.Add(new PermissaoTrabalhoEpi
            {
                PermissaoTrabalhoId = request.PermissaoTrabalhoId,
                Item = item.Item,
                Complemento = item.Complemento,
            });
        }

        pt.OutrosEpis = request.OutrosEpis;

        await _db.SaveChangesAsync(ct);
    }
}
