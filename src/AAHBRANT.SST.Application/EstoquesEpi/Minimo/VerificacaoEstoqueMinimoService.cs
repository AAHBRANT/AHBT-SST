using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Minimo;

public interface IVerificacaoEstoqueMinimoService
{
    // obraId nulo = todas as obras com algum mínimo definido (varredura periódica do Worker).
    Task VerificarAsync(Guid? obraId, CancellationToken ct = default);
}

// Alerta de estoque mínimo (especificação 21/10/2026, Módulo 2): quando o disponível de um EPI fica
// abaixo do mínimo (ou zera), os técnicos da obra recebem um alerta e uma mensagem no Teams — uma vez
// só por item, até ele voltar a ficar em dia, quando o alerta é encerrado sozinho. Chamado logo depois
// de cada movimento de estoque e, como rede de segurança, na varredura periódica do Worker.
public class VerificacaoEstoqueMinimoService : IVerificacaoEstoqueMinimoService
{
    public const string OrigemAlerta = nameof(EstoqueEpi);

    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicos;
    private readonly IFilaNotificacaoTeams _filaTeams;

    public VerificacaoEstoqueMinimoService(IAppDbContext db, ITecnicosSegurancaPorObraService tecnicos, IFilaNotificacaoTeams filaTeams)
    {
        _db = db;
        _tecnicos = tecnicos;
        _filaTeams = filaTeams;
    }

    public async Task VerificarAsync(Guid? obraId, CancellationToken ct = default)
    {
        var obras = obraId is { } id
            ? new List<Guid> { id }
            : await _db.EstoquesEpi.Where(e => e.EstoqueMinimo != null).Select(e => e.ObraId).Distinct().ToListAsync(ct);

        foreach (var obra in obras)
            await VerificarObraAsync(obra, ct);
    }

    private async Task VerificarObraAsync(Guid obraId, CancellationToken ct)
    {
        var itens = await CalculadoraEstoqueEpi.CalcularAsync(_db, obraId, ct, comTransferencias: false);
        var abertos = await _db.Alertas
            .Where(a => a.Tipo == TipoAlerta.EstoqueEpiAbaixoMinimo && a.ObraId == obraId
                && (a.Status == StatusAlerta.Aberto || a.Status == StatusAlerta.EmTratamento || a.Status == StatusAlerta.Escalonado))
            .ToListAsync(ct);

        var obraNome = await _db.Obras.Where(o => o.Id == obraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? "";
        var novos = new List<Alerta>();
        List<Guid>? tecnicos = null;

        foreach (var item in itens.Where(i => i.EstoqueEpiId is not null && i.EstoqueMinimo is > 0))
        {
            var doItem = abertos.Where(a => a.EntidadeOrigemId == item.EstoqueEpiId).ToList();
            if (!SituacaoEstoqueEpi.PedeReposicao(item.Situacao))
            {
                foreach (var a in doItem) a.Status = StatusAlerta.Resolvido;
                continue;
            }
            if (doItem.Count > 0) continue;

            tecnicos ??= await _tecnicos.ObterUsuarioIdsAsync(obraId, ct);
            var zerado = item.Situacao == SituacaoEstoqueEpi.Zerado;
            var titulo = zerado
                ? $"EPI zerado: {item.Nome} · {obraNome}"
                : $"EPI abaixo do mínimo: {item.Nome} · {obraNome}";
            var tamanhos = item.TamanhosEmFalta.Count > 0 ? $" Tamanhos em falta: {string.Join(", ", item.TamanhosEmFalta)}." : "";
            var descricao = $"Disponível {Math.Max(0, item.Disponivel)} (saldo {item.Saldo}, separado para kits {item.Reservado}), mínimo {item.EstoqueMinimo}. " +
                $"Repor {item.QuantidadeSugeridaCompra} unidade(s).{tamanhos} Gere a solicitação de reposição na aba Estoque.";
            novos.AddRange(tecnicos.Select(usuarioId => new Alerta
            {
                Tipo = TipoAlerta.EstoqueEpiAbaixoMinimo,
                Severidade = zerado ? SeveridadeAlerta.Critico : SeveridadeAlerta.Atencao,
                Titulo = titulo,
                Descricao = descricao,
                EntidadeOrigemTipo = OrigemAlerta,
                EntidadeOrigemId = item.EstoqueEpiId!.Value,
                ObraId = obraId,
                DestinatarioUsuarioId = usuarioId,
            }));
        }

        _db.Alertas.AddRange(novos);
        await _db.SaveChangesAsync(ct);
        foreach (var alerta in novos)
            await _filaTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao), ct);
    }
}
