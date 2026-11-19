using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Bloqueia encerramento enquanto houver item sem resposta — aplicação do mesmo princípio de
// bloqueio preventivo já usado em AutorizarPermissaoTrabalhoCommand; não é uma regra literal do
// §23 isoladamente, mas garante que "cada inspeção deverá gerar evidência" (§23) tenha, no
// mínimo, todo item do checklist respondido antes de fechar.
//
// Motor de Assinatura Eletrônica — mesma decisão tomada em EncerrarNaoConformidadeCommand: garante
// (idempotente) um DocumentoAssinatura para a inspeção, sem bloquear o encerramento por ele ainda
// não estar Finalizado (mesmo padrão não-bloqueante já usado por EncerrarDdsCommand).
public record EncerrarInspecaoCommand(Guid Id) : IRequest;

public class EncerrarInspecaoCommandValidator : AbstractValidator<EncerrarInspecaoCommand>
{
    public EncerrarInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarInspecaoCommandHandler : IRequestHandler<EncerrarInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes
            .Include(i => i.Respostas).ThenInclude(r => r.ChecklistModeloItem)
            .Include(i => i.ItemInspecionavel)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");

        var pendentes = inspecao.Respostas.Where(r => r.Ativo && r.StatusItem == null).ToList();
        if (pendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: {pendentes.Count} item(ns) do checklist ainda não respondido(s).");

        // 18/11/2026 (decisão do usuário): TODA inspeção exige 2 fotos em CADA item do checklist, qualquer que
        // seja o tipo e a resposta. Substitui as regras anteriores (ExigeFotografia do modelo, Tema com 1 foto em
        // todos os itens, Veículo só no Não Conforme). Foto 1 = InspecaoItemResposta.FotoConteudo (nunca null,
        // por isso a checagem é por Length); foto 2 = InspecaoItemFotoAdicional ativa.
        var respostasAtivas = inspecao.Respostas.Where(r => r.Ativo).ToList();
        var idsRespostas = respostasAtivas.Select(r => r.Id).ToList();
        var comSegundaFoto = (await _db.InspecaoItemFotosAdicionais
            .Where(f => f.Ativo && idsRespostas.Contains(f.InspecaoItemRespostaId) && f.FotoConteudo.Length > 0)
            .Select(f => f.InspecaoItemRespostaId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var semDuasFotos = respostasAtivas
            .Where(r => r.FotoConteudo.Length == 0 || !comSegundaFoto.Contains(r.Id))
            .OrderBy(r => r.ChecklistModeloItem?.Ordem ?? int.MaxValue)
            .Select(r => r.DescricaoPersonalizada ?? r.ChecklistModeloItem?.Descricao ?? "Item")
            .ToList();

        if (semDuasFotos.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: cada item do checklist exige 2 fotos. Faltam fotos em: {string.Join("; ", semDuasFotos)}");

        foreach (var resposta in inspecao.Respostas.Where(r => r.Ativo))
        {
            if (resposta.FotoConteudo.Length > 0)
                AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(resposta.FotoMetadadosJson, $"Item {resposta.ChecklistModeloItem?.Ordem} — foto anterior");
            if (resposta.FotoDepoisConteudo is { Length: > 0 })
                AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(resposta.FotoDepoisMetadadosJson, $"Item {resposta.ChecklistModeloItem?.Ordem} — foto posterior");
        }

        // A inspeção só vira Concluída quando assinada (FinalizarDocumentoCommand). Até lá fica
        // travada em "Aguardando assinatura"; ReabrirInspecaoCommand devolve para ajustes se ninguém assinou.
        inspecao.Status = StatusInspecao.AguardandoAssinatura;

        // Item Não Conforme sem NC vira NC automaticamente no encerramento (decisão do usuário, 17/10):
        // basta um item para gerar; uma NC por item. Itens que já tiveram "Gerar ocorrência" não duplicam.
        var naoConformes = inspecao.Respostas.Where(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme).ToList();
        if (naoConformes.Count > 0)
        {
            var ids = naoConformes.Select(r => r.Id).ToList();
            var jaTemNc = await _db.NaoConformidades.IgnoreQueryFilters()
                .Where(n => n.InspecaoItemRespostaId != null && ids.Contains(n.InspecaoItemRespostaId.Value))
                .Select(n => n.InspecaoItemRespostaId!.Value).ToListAsync(ct);
            foreach (var r in naoConformes.Where(r => !jaTemNc.Contains(r.Id)))
            {
                r.Inspecao = inspecao;
                _db.NaoConformidades.Add(NaoConformidades.Commands.CriarNaoConformidadeDeItemCommandHandler.NovaDoItem(r));
            }
        }

        var documentoExistente = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.Inspecao) && d.EntidadeId == inspecao.Id, ct);
        if (documentoExistente is null)
            _db.DocumentosAssinatura.Add(new DocumentoAssinatura
            {
                EntidadeTipo = nameof(Domain.Entidades.Inspecao),
                EntidadeId = inspecao.Id,
            });

        await _db.SaveChangesAsync(ct);

        // Ações do plano de ocorrências desta obra que se resolvem com uma inspeção deste tipo.
        var ocorrencias = await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.OcorrenciasComInspecaoAsync(_db, inspecao.ObraId, inspecao.TipoInspecao, inspecao.ItemInspecionavel?.TemaInspecaoId, ct);
        await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(_db, ocorrencias, ct);
    }
}
