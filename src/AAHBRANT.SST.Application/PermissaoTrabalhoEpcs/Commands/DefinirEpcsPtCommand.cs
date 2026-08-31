using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoEpcs.Commands;

// §5 do formulário, "EPCs / recursos aplicáveis" — mesmo princípio de DefinirTiposTrabalhoPtCommand.
// OutrosEpcs é o campo "Outros EPCs/recursos: ____" do mesmo bloco.
public record DefinirEpcsPtCommand(Guid PermissaoTrabalhoId, List<ItemEpcPt> Itens, string? OutrosEpcs) : IRequest;

public class DefinirEpcsPtCommandValidator : AbstractValidator<DefinirEpcsPtCommand>
{
    public DefinirEpcsPtCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleFor(x => x.OutrosEpcs).MaximumLength(300);
        RuleForEach(x => x.Itens).IsInEnum();
    }
}

public class DefinirEpcsPtCommandHandler : IRequestHandler<DefinirEpcsPtCommand>
{
    private readonly IAppDbContext _db;

    public DefinirEpcsPtCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirEpcsPtCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.PermissaoTrabalhoId, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var atuais = await _db.PermissaoTrabalhoEpcs
            .Where(e => e.PermissaoTrabalhoId == request.PermissaoTrabalhoId).ToListAsync(ct);
        _db.PermissaoTrabalhoEpcs.RemoveRange(atuais);

        foreach (var item in request.Itens.Distinct())
        {
            _db.PermissaoTrabalhoEpcs.Add(new PermissaoTrabalhoEpc
            {
                PermissaoTrabalhoId = request.PermissaoTrabalhoId,
                Item = item,
            });
        }

        pt.OutrosEpcs = request.OutrosEpcs;

        await _db.SaveChangesAsync(ct);
    }
}
