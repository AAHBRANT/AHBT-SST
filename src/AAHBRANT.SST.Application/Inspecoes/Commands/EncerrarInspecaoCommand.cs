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
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");

        var pendentes = inspecao.Respostas.Where(r => r.Ativo && r.StatusItem == null).ToList();
        if (pendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: {pendentes.Count} item(ns) do checklist ainda não respondido(s).");

        // Task 6 (Alojamento em Inspeções, 2026-09-15): item com ChecklistModeloItem.ExigeFotografia
        // bloqueia o encerramento enquanto não tiver foto registrada. FotoConteudo nunca é null
        // (default Array.Empty<byte>() em InspecaoItemResposta) — por isso a checagem é por Length.
        var itensSemFotoObrigatoria = inspecao.Respostas
            .Where(r => r.Ativo && (r.ChecklistModeloItem?.ExigeFotografia ?? false) && r.FotoConteudo.Length == 0)
            .Select(r => r.DescricaoPersonalizada ?? r.ChecklistModeloItem!.Descricao)
            .ToList();

        if (itensSemFotoObrigatoria.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: os seguintes itens exigem foto e ainda não têm: {string.Join("; ", itensSemFotoObrigatoria)}");

        // Veículos e temas criados pelo usuário: 1 foto, obrigatória APENAS nos itens marcados como Não
        // Conforme (decisão do usuário, 02/10/2026). Item Conforme/Não Aplicável não exige nem usa foto.
        if (inspecao.TipoInspecao is TipoInspecao.Veiculo or TipoInspecao.Tema)
        {
            var ncSemFoto = inspecao.Respostas
                .Where(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme && r.FotoConteudo.Length == 0)
                .Select(r => r.DescricaoPersonalizada ?? r.ChecklistModeloItem!.Descricao)
                .ToList();

            if (ncSemFoto.Count > 0)
                throw new InvalidOperationException(
                    $"Não é possível encerrar: os itens Não Conforme a seguir exigem foto e ainda não têm: {string.Join("; ", ncSemFoto)}");
        }

        if (inspecao.TipoInspecao == TipoInspecao.Alojamento)
        {
            var itensSemFotoPosteriorObrigatoria = inspecao.Respostas
                .Where(r => r.Ativo && (r.ChecklistModeloItem?.ExigeFotografia ?? false) && (r.FotoDepoisConteudo?.Length ?? 0) == 0)
                .Select(r => r.DescricaoPersonalizada ?? r.ChecklistModeloItem!.Descricao)
                .ToList();

            if (itensSemFotoPosteriorObrigatoria.Count > 0)
                throw new InvalidOperationException(
                    $"Não é possível encerrar: os seguintes itens de Alojamento exigem evidência posterior e ainda não têm: {string.Join("; ", itensSemFotoPosteriorObrigatoria)}");
        }

        foreach (var resposta in inspecao.Respostas.Where(r => r.Ativo))
        {
            if (resposta.FotoConteudo.Length > 0)
                AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(resposta.FotoMetadadosJson, $"Item {resposta.ChecklistModeloItem?.Ordem} — foto anterior");
            if (resposta.FotoDepoisConteudo is { Length: > 0 })
                AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(resposta.FotoDepoisMetadadosJson, $"Item {resposta.ChecklistModeloItem?.Ordem} — foto posterior");
        }

        inspecao.Status = StatusInspecao.Concluida;

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
        var ocorrencias = await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.OcorrenciasComInspecaoAsync(_db, inspecao.ObraId, inspecao.TipoInspecao, ct);
        await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(_db, ocorrencias, ct);
    }
}
