using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record DefinirMatrizTreinamentoFuncaoCommand(Guid FuncaoId, List<Guid> CursoTreinamentoIds) : IRequest;

public class DefinirMatrizTreinamentoFuncaoCommandValidator : AbstractValidator<DefinirMatrizTreinamentoFuncaoCommand>
{
    public DefinirMatrizTreinamentoFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.CursoTreinamentoIds).NotNull();
        RuleForEach(x => x.CursoTreinamentoIds).NotEmpty();
    }
}

public class DefinirMatrizTreinamentoFuncaoCommandHandler : IRequestHandler<DefinirMatrizTreinamentoFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public DefinirMatrizTreinamentoFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirMatrizTreinamentoFuncaoCommand request, CancellationToken ct)
    {
        var funcaoExiste = await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct);
        if (!funcaoExiste)
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        // IgnoreQueryFilters: precisa enxergar também vínculos previamente desativados (Ativo=false)
        // para reativá-los em vez de tentar inserir duplicata e violar o índice único
        // (FuncaoId, CursoTreinamentoId).
        var vinculosAtuais = await _db.MatrizTreinamentoFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == request.FuncaoId)
            .ToListAsync(ct);

        var idsDesejados = request.CursoTreinamentoIds.Distinct().ToHashSet();

        foreach (var vinculo in vinculosAtuais)
            vinculo.Ativo = idsDesejados.Contains(vinculo.CursoTreinamentoId);

        var idsExistentes = vinculosAtuais.Select(v => v.CursoTreinamentoId).ToHashSet();
        foreach (var cursoTreinamentoId in idsDesejados.Where(id => !idsExistentes.Contains(id)))
        {
            _db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao
            {
                FuncaoId = request.FuncaoId,
                CursoTreinamentoId = cursoTreinamentoId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
