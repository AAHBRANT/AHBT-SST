using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

// Pedido do usuário (22/09): funções duplicadas (mesmo nome, uma com CBO — vinda do G-RH — e outra
// sem, criada manualmente antes da integração) migram todas as referências da "sem CBO" para a "com
// CBO" e só então a duplicata vazia é removida. Nunca mexe em função sem CBO que não seja duplicata
// de nenhuma outra (essas são funções reais, só nunca sincronizadas com o G-RH).
//
// Todas as FKs de Funcao usam DeleteBehavior.Restrict (ver Configuracoes/*.cs) — se sobrar alguma
// referência que este comando não migrou, o Remove() final falha em vez de corromper dado; rede de
// segurança do próprio banco, não só deste código.
public record MesclarFuncaoDuplicadaCommand(Guid FuncaoManterId, Guid FuncaoRemoverId) : IRequest;

public class MesclarFuncaoDuplicadaCommandValidator : AbstractValidator<MesclarFuncaoDuplicadaCommand>
{
    public MesclarFuncaoDuplicadaCommandValidator()
    {
        RuleFor(x => x.FuncaoManterId).NotEmpty();
        RuleFor(x => x.FuncaoRemoverId).NotEmpty();
        RuleFor(x => x).Must(x => x.FuncaoManterId != x.FuncaoRemoverId)
            .WithMessage("A função a manter e a função a remover não podem ser a mesma.");
    }
}

public class MesclarFuncaoDuplicadaCommandHandler : IRequestHandler<MesclarFuncaoDuplicadaCommand>
{
    private readonly IAppDbContext _db;

    public MesclarFuncaoDuplicadaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(MesclarFuncaoDuplicadaCommand request, CancellationToken ct)
    {
        var manter = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.FuncaoManterId, ct)
            ?? throw new KeyNotFoundException($"Função {request.FuncaoManterId} não encontrada.");
        var remover = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.FuncaoRemoverId, ct)
            ?? throw new KeyNotFoundException($"Função {request.FuncaoRemoverId} não encontrada.");

        if (string.IsNullOrWhiteSpace(manter.CboCodigo))
            throw new InvalidOperationException("A função a manter precisa ter um Código CBO preenchido.");
        if (manter.Nome != remover.Nome)
            throw new InvalidOperationException("As duas funções precisam ter exatamente o mesmo nome para serem mescladas.");

        // Trabalhador: reatribuição direta, sem risco de duplicar (FuncaoId não é chave única).
        var trabalhadores = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.FuncaoId == remover.Id).ToListAsync(ct);
        foreach (var t in trabalhadores) t.FuncaoId = manter.Id;

        // Matrizes (EPI/Treinamento/Uniforme): têm índice único (FuncaoId, ItemId) — se "manter" já
        // tem o mesmo item, a linha de "remover" é descartada (já é redundante); senão, reaponta.
        var epiManterIds = await _db.MatrizEpiFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == manter.Id).Select(m => m.CatalogoEpiId).ToListAsync(ct);
        var epiRemover = await _db.MatrizEpiFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == remover.Id).ToListAsync(ct);
        foreach (var m in epiRemover)
        {
            if (epiManterIds.Contains(m.CatalogoEpiId)) _db.MatrizEpiFuncoes.Remove(m);
            else m.FuncaoId = manter.Id;
        }

        var treinoManterIds = await _db.MatrizTreinamentoFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == manter.Id).Select(m => m.CursoTreinamentoId).ToListAsync(ct);
        var treinoRemover = await _db.MatrizTreinamentoFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == remover.Id).ToListAsync(ct);
        foreach (var m in treinoRemover)
        {
            if (treinoManterIds.Contains(m.CursoTreinamentoId)) _db.MatrizTreinamentoFuncoes.Remove(m);
            else m.FuncaoId = manter.Id;
        }

        var uniformeManterIds = await _db.MatrizUniformeFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == manter.Id).Select(m => m.CatalogoUniformeId).ToListAsync(ct);
        var uniformeRemover = await _db.MatrizUniformeFuncoes.IgnoreQueryFilters()
            .Where(m => m.FuncaoId == remover.Id).ToListAsync(ct);
        foreach (var m in uniformeRemover)
        {
            if (uniformeManterIds.Contains(m.CatalogoUniformeId)) _db.MatrizUniformeFuncoes.Remove(m);
            else m.FuncaoId = manter.Id;
        }

        // RequisitoLegalCriterio: FuncaoId é um dos critérios opcionais de aplicabilidade (nullable),
        // sem índice único conhecido — reatribuição direta.
        var criterios = await _db.RequisitoLegalCriterios.IgnoreQueryFilters()
            .Where(r => r.FuncaoId == remover.Id).ToListAsync(ct);
        foreach (var r in criterios) r.FuncaoId = manter.Id;

        await _db.SaveChangesAsync(ct);

        // Descarta o rastreamento antes de excluir: uma matriz redundante descartada acima (já
        // existia em "manter") continua com FuncaoId apontando pra "remover" — mesmo persistida como
        // Ativo=false (soft-delete, ver SstDbContext.AplicarAuditoria), o EF Core recusa remover
        // "remover" enquanto QUALQUER entidade rastreada nesta mesma instância de contexto ainda
        // referencia o Id dela (relação obrigatória, sem cascade). Limpar o rastreamento e buscar
        // "remover" de novo garante que nada além dela mesma está em memória nesse momento.
        _db.DescartarAlteracoesPendentes();
        var removerParaExcluir = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == remover.Id, ct)
            ?? throw new KeyNotFoundException($"Função {remover.Id} não encontrada.");

        // Só remove depois que as referências migraram e foram salvas — se sobrou alguma referência
        // que este comando não previu, o Restrict do FK derruba este SaveChanges (não o de cima),
        // deixando os dados já migrados intactos em vez de uma exclusão parcial/corrompida.
        _db.Funcoes.Remove(removerParaExcluir);
        await _db.SaveChangesAsync(ct);
    }
}
