using System.Text.Json;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.NaoConformidades;

// Monta o evento do contrato do G-QUALI (C:\Projetos\GQUALI-APP\docs\integracoes\nc-externas.md) a partir da
// NC. Só NC com obra (o G-QUALI precisa do código da obra; código sem correspondência cai na triagem de lá).
// LGPD: envia descrição, local, prazo e situação — sem responsável, trabalhador ou dados de saúde. A descrição
// é texto livre: o DPO deve validar o espelhamento (pendência registrada).
public static class EventoNcGQualiFactory
{
    public const string Criada = "criada", Atualizada = "atualizada", Encerrada = "encerrada", Cancelada = "cancelada";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static long _ultimaRevisao;

    public static string RotuloStatus(StatusNaoConformidade s) => s switch
    {
        StatusNaoConformidade.Aberta => "Aberta",
        StatusNaoConformidade.Enviada => "Enviada",
        StatusNaoConformidade.EmAnalise => "Em análise",
        StatusNaoConformidade.EmAndamento => "Em andamento",
        StatusNaoConformidade.AguardandoValidacao => "Aguardando validação",
        StatusNaoConformidade.Devolvida => "Devolvida",
        StatusNaoConformidade.Encerrada => "Encerrada",
        _ => s.ToString(),
    };

    // Revisão crescente (ticks do relógio, nunca repetida no processo); o G-QUALI ignora revisão mais antiga.
    public static long ProximaRevisao(DateTime agoraUtc)
    {
        while (true)
        {
            var atual = Interlocked.Read(ref _ultimaRevisao);
            var nova = Math.Max(agoraUtc.Ticks, atual + 1);
            if (Interlocked.CompareExchange(ref _ultimaRevisao, nova, atual) == atual) return nova;
        }
    }

    public static string Titulo(string descricao)
    {
        var linha = descricao.Split('\n', 2)[0].Trim();
        return linha.Length <= 200 ? linha : linha[..197] + "...";
    }

    public static EventoNcGQuali Criar(NaoConformidade nc, string obraCodigo, string tipo, DateTime agoraUtc)
    {
        var revisao = ProximaRevisao(agoraUtc);
        var eventId = $"gsst-{nc.Id:N}-{revisao}";
        var descricao = string.IsNullOrWhiteSpace(nc.RequisitoRelacionado)
            ? nc.Descricao
            : $"{nc.Descricao}\nRequisito: {nc.RequisitoRelacionado}";
        var payload = new
        {
            eventId,
            eventType = tipo,
            sourceSystem = "GSst",
            sourceId = nc.Id.ToString(),
            sourceRevision = revisao,
            occurredAt = agoraUtc,
            obraCodigo,
            titulo = Titulo(nc.Descricao),
            descricao = descricao.Length <= 2000 ? descricao : descricao[..2000],
            local = nc.Local,
            prazo = nc.Prazo is { } p ? DateOnly.FromDateTime(p) : (DateOnly?)null,
            statusOrigem = RotuloStatus(nc.Status),
            disciplina = "Segurança do Trabalho",
        };
        return new EventoNcGQuali
        {
            NaoConformidadeId = nc.Id,
            EventId = eventId,
            Tipo = tipo,
            Revisao = revisao,
            Payload = JsonSerializer.Serialize(payload, Json),
            CriadoEmUtc = agoraUtc,
            ProximaTentativaEmUtc = agoraUtc,
        };
    }
}
