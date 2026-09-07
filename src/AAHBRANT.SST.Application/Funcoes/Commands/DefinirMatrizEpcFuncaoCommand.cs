using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizEpcFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpcIds) : IRequest;

public class DefinirMatrizEpcFuncaoCommandValidator : AbstractValidator<DefinirMatrizEpcFuncaoCommand>
{
    public DefinirMatrizEpcFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CatalogoEpcIds).NotNull();
        RuleForEach(x => x.CatalogoEpcIds).NotEmpty();
    }
}

public class DefinirMatrizEpcFuncaoCommandHandler : IRequestHandler<DefinirMatrizEpcFuncaoCommand>
{
    private readonly IAppDbContext _db;
    public DefinirMatrizEpcFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizEpcFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CatalogoEpcId). Mesmo padrão de DefinirMatrizUniformeFuncaoCommand.
        var vinculosAtuais = await _db.MatrizEpcFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CatalogoEpcIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CatalogoEpcId);

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoEpcId).ToHashSet();
        foreach (var catalogoEpcId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizEpcFuncoes.Add(new MatrizEpcFuncao
            {
                FuncaoId = request.FuncaoId,
                CatalogoEpcId = catalogoEpcId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
