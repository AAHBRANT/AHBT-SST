using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizEpiFuncaoCommand(Guid FuncaoId, List<Guid> CatalogoEpiIds) : IRequest;

public class DefinirMatrizEpiFuncaoCommandValidator : AbstractValidator<DefinirMatrizEpiFuncaoCommand>
{
    public DefinirMatrizEpiFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CatalogoEpiIds).NotNull();
        RuleForEach(x => x.CatalogoEpiIds).NotEmpty();
    }
}

public class DefinirMatrizEpiFuncaoCommandHandler : IRequestHandler<DefinirMatrizEpiFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public DefinirMatrizEpiFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizEpiFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CatalogoEpiId).
        var vinculosAtuais = await _db.MatrizEpiFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CatalogoEpiIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CatalogoEpiId);

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoEpiId).ToHashSet();
        foreach (var catalogoEpiId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao
            {
                FuncaoId = request.FuncaoId,
                CatalogoEpiId = catalogoEpiId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
