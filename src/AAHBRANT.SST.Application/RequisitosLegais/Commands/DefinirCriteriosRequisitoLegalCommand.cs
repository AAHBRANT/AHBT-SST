using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Commands;

public record CriterioAplicabilidadeInput(
    TipoCriterioAplicabilidade Tipo,
    Guid? PerigoId,
    Guid? FuncaoId,
    TipoAtivo? TipoEquipamento,
    Guid? ItemQuestionarioAplicabilidadeId);

// Substitui todos os critérios do requisito pela lista informada — mesmo padrão "replace-all
// idempotente" de DefinirMatrizEpiFuncaoCommand (reativa/desativa em vez de recriar), adaptado
// porque aqui a chave de identidade de um critério é uma tupla (Tipo + a referência daquele Tipo),
// não um único Guid.
public record DefinirCriteriosRequisitoLegalCommand(Guid RequisitoLegalId, List<CriterioAplicabilidadeInput> Criterios) : IRequest;

public class DefinirCriteriosRequisitoLegalCommandValidator : AbstractValidator<DefinirCriteriosRequisitoLegalCommand>
{
    public DefinirCriteriosRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.RequisitoLegalId).NotEmpty();
        RuleFor(x => x.Criterios).NotNull();
        RuleForEach(x => x.Criterios).ChildRules(criterio =>
        {
            criterio.RuleFor(c => c).Must(TerReferenciaCorrespondenteAoTipo)
                .WithMessage("Cada critério deve preencher exatamente a referência correspondente ao seu Tipo.");
        });
    }

    private static bool TerReferenciaCorrespondenteAoTipo(CriterioAplicabilidadeInput c) => c.Tipo switch
    {
        TipoCriterioAplicabilidade.Perigo => c.PerigoId.HasValue,
        TipoCriterioAplicabilidade.Funcao => c.FuncaoId.HasValue,
        TipoCriterioAplicabilidade.Equipamento => c.TipoEquipamento.HasValue,
        TipoCriterioAplicabilidade.ItemQuestionario => c.ItemQuestionarioAplicabilidadeId.HasValue,
        _ => false,
    };
}

public class DefinirCriteriosRequisitoLegalCommandHandler : IRequestHandler<DefinirCriteriosRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public DefinirCriteriosRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    private static string Chave(TipoCriterioAplicabilidade tipo, Guid? perigoId, Guid? funcaoId, TipoAtivo? tipoEquipamento, Guid? itemId)
        => $"{tipo}|{perigoId}|{funcaoId}|{tipoEquipamento}|{itemId}";

    public async Task Handle(DefinirCriteriosRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisitoExiste = await _db.RequisitosLegais.AnyAsync(r => r.Id == request.RequisitoLegalId, ct);
        if (!requisitoExiste)
            throw new KeyNotFoundException($"Requisito legal {request.RequisitoLegalId} não encontrado.");

        foreach (var c in request.Criterios)
        {
            if (c.PerigoId.HasValue && !await _db.Perigos.AnyAsync(p => p.Id == c.PerigoId, ct))
                throw new KeyNotFoundException($"Perigo {c.PerigoId} não encontrado.");
            if (c.FuncaoId.HasValue && !await _db.Funcoes.AnyAsync(f => f.Id == c.FuncaoId, ct))
                throw new KeyNotFoundException($"Função {c.FuncaoId} não encontrada.");
            if (c.ItemQuestionarioAplicabilidadeId.HasValue &&
                !await _db.ItensQuestionarioAplicabilidade.AnyAsync(i => i.Id == c.ItemQuestionarioAplicabilidadeId, ct))
                throw new KeyNotFoundException($"Item de questionário {c.ItemQuestionarioAplicabilidadeId} não encontrado.");
        }

        // IgnoreQueryFilters: precisa enxergar também critérios previamente desativados para
        // reativá-los em vez de criar duplicata — mesmo princípio de DefinirMatrizEpiFuncaoCommand.
        var criteriosAtuais = await _db.RequisitoLegalCriterios.IgnoreQueryFilters()
            .Where(c => c.RequisitoLegalId == request.RequisitoLegalId)
            .ToListAsync(ct);

        var desejados = request.Criterios
            .GroupBy(c => Chave(c.Tipo, c.PerigoId, c.FuncaoId, c.TipoEquipamento, c.ItemQuestionarioAplicabilidadeId))
            .Select(g => g.First())
            .ToList();
        var chavesDesejadas = desejados.Select(c => Chave(c.Tipo, c.PerigoId, c.FuncaoId, c.TipoEquipamento, c.ItemQuestionarioAplicabilidadeId)).ToHashSet();

        foreach (var atual in criteriosAtuais)
        {
            var chaveAtual = Chave(atual.Tipo, atual.PerigoId, atual.FuncaoId, atual.TipoEquipamento, atual.ItemQuestionarioAplicabilidadeId);
            atual.Ativo = chavesDesejadas.Contains(chaveAtual);
        }

        var chavesExistentes = criteriosAtuais
            .Select(c => Chave(c.Tipo, c.PerigoId, c.FuncaoId, c.TipoEquipamento, c.ItemQuestionarioAplicabilidadeId))
            .ToHashSet();

        foreach (var c in desejados)
        {
            var chave = Chave(c.Tipo, c.PerigoId, c.FuncaoId, c.TipoEquipamento, c.ItemQuestionarioAplicabilidadeId);
            if (chavesExistentes.Contains(chave)) continue;

            _db.RequisitoLegalCriterios.Add(new RequisitoLegalCriterio
            {
                RequisitoLegalId = request.RequisitoLegalId,
                Tipo = c.Tipo,
                PerigoId = c.PerigoId,
                FuncaoId = c.FuncaoId,
                TipoEquipamento = c.TipoEquipamento,
                ItemQuestionarioAplicabilidadeId = c.ItemQuestionarioAplicabilidadeId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
