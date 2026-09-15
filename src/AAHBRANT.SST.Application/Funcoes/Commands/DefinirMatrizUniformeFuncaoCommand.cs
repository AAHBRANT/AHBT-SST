using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizUniformeFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoUniformeIds) : IRequest;

public class DefinirMatrizUniformeFuncaoCommandValidator : AbstractValidator<DefinirMatrizUniformeFuncaoCommand>
{
    public DefinirMatrizUniformeFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CatalogoUniformeIds).NotNull();
        RuleForEach(x => x.CatalogoUniformeIds).NotEmpty();
    }
}

public class DefinirMatrizUniformeFuncaoCommandHandler : IRequestHandler<DefinirMatrizUniformeFuncaoCommand>
{
    private readonly IAppDbContext _db;
    public DefinirMatrizUniformeFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizUniformeFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CatalogoUniformeId). Mesmo padrão de DefinirMatrizEpiFuncaoCommand.
        var vinculosAtuais = await _db.MatrizUniformeFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CatalogoUniformeIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CatalogoUniformeId);

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoUniformeId).ToHashSet();
        foreach (var catalogoUniformeId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao
            {
                FuncaoId = request.FuncaoId,
                CatalogoUniformeId = catalogoUniformeId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
