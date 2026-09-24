using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Inspeção não tinha exclusão até 24/09. Criada pelo mesmo motivo dos outros módulos — limpar o que
// foi lançado testando —, mas com uma diferença que decidiu o desenho: uma inspeção não é um
// registro isolado. Ela carrega as respostas de cada item do checklist e, a partir de um item
// reprovado, o técnico gera uma Não Conformidade (CriarNaoConformidadeDeItemCommand), que aponta
// para a resposta em NaoConformidade.InspecaoItemRespostaId.
//
// A NC tem vida própria: prazo, responsável, plano de ação, e é acompanhada fora da inspeção.
// Apagar a inspeção em cascata levaria junto esse trabalho sem avisar; deixar a NC apontando para
// uma resposta que não existe mais quebraria a origem dela. Então a exclusão é RECUSADA enquanto
// houver NC vinculada, dizendo quantas são — o caminho é excluir (ou resolver) as não
// conformidades primeiro, o que a tela de Não conformidades já permite.
public record ExcluirInspecaoCommand(Guid Id) : IRequest;

public class ExcluirInspecaoCommandValidator : AbstractValidator<ExcluirInspecaoCommand>
{
    public ExcluirInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirInspecaoCommandHandler : IRequestHandler<ExcluirInspecaoCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Inspeção não encontrada.");

        var respostas = await _db.InspecaoItemRespostas
            .Where(r => r.InspecaoId == inspecao.Id)
            .ToListAsync(ct);

        var respostaIds = respostas.Select(r => r.Id).ToList();
        var naoConformidadesVinculadas = await _db.NaoConformidades
            .CountAsync(nc => nc.InspecaoItemRespostaId != null && respostaIds.Contains(nc.InspecaoItemRespostaId.Value), ct);

        if (naoConformidadesVinculadas > 0)
            throw new InvalidOperationException(
                $"Esta inspeção gerou {naoConformidadesVinculadas} não conformidade(s) e por isso não pode ser excluída. " +
                "Exclua ou conclua essas não conformidades primeiro — elas têm prazo e responsável próprios, e seriam perdidas junto.");

        // As respostas pertencem à inspeção e saem com ela (soft delete: SstDbContext marca
        // Ativo=false). Sem isto ficariam ativas apontando para uma inspeção que não aparece mais.
        foreach (var resposta in respostas)
            _db.InspecaoItemRespostas.Remove(resposta);

        _db.Inspecoes.Remove(inspecao);
        await _db.SaveChangesAsync(ct);
    }
}
