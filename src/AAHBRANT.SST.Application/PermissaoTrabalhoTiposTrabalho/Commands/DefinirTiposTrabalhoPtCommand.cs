using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoTiposTrabalho.Commands;

public record TipoTrabalhoInput(TipoTrabalhoEspecialPt Tipo, string? DescricaoOutro);

// §3 do formulário — multi-select: só os tipos marcados viram linha (mesmo princípio de
// MatrizEpiFuncao, mas substitui tudo de uma vez em vez de reativar/desativar, já que nenhum outro
// módulo referencia estas linhas). "Outro" carrega DescricaoOutro; os demais tipos ignoram esse campo.
public record DefinirTiposTrabalhoPtCommand(Guid PermissaoTrabalhoId, List<TipoTrabalhoInput> Tipos) : IRequest;

public class DefinirTiposTrabalhoPtCommandValidator : AbstractValidator<DefinirTiposTrabalhoPtCommand>
{
    public DefinirTiposTrabalhoPtCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.Tipos).NotNull();
        RuleForEach(x => x.Tipos).ChildRules(tipo =>
        {
            tipo.RuleFor(t => t.Tipo).IsInEnum();
            tipo.RuleFor(t => t.DescricaoOutro).MaximumLength(200);
        });
    }
}

public class DefinirTiposTrabalhoPtCommandHandler : IRequestHandler<DefinirTiposTrabalhoPtCommand>
{
    private readonly IAppDbContext _db;

    public DefinirTiposTrabalhoPtCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirTiposTrabalhoPtCommand request, CancellationToken ct)
    {
        var ptExiste = await _db.PermissoesTrabalho.AnyAsync(p => p.Id == request.PermissaoTrabalhoId, ct);
        if (!ptExiste)
            throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var atuais = await _db.PermissaoTrabalhoTiposTrabalho
            .Where(t => t.PermissaoTrabalhoId == request.PermissaoTrabalhoId).ToListAsync(ct);
        _db.PermissaoTrabalhoTiposTrabalho.RemoveRange(atuais);

        foreach (var tipo in request.Tipos.GroupBy(t => t.Tipo).Select(g => g.First()))
        {
            _db.PermissaoTrabalhoTiposTrabalho.Add(new PermissaoTrabalhoTipoTrabalho
            {
                PermissaoTrabalhoId = request.PermissaoTrabalhoId,
                Tipo = tipo.Tipo,
                DescricaoOutro = tipo.Tipo == TipoTrabalhoEspecialPt.Outro ? tipo.DescricaoOutro : null,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
