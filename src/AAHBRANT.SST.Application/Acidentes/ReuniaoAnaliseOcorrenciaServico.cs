using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Regras da reunião de análise compartilhadas entre o registro por relato (CriarAcidenteCommand) e
// a tela da ocorrência (ReuniaoAnaliseCommands): a reunião vira ação do plano e registro próprio, e o
// Teams só é chamado depois do registro gravado — falha no Teams nunca desfaz o que foi salvo.
public static class ReuniaoAnaliseOcorrenciaServico
{
    public static async Task<(ReuniaoAnaliseOcorrencia Entidade, List<string> Emails)> RegistrarAsync(
        IAppDbContext db, Acidente acidente, DateTime inicio, int duracaoMinutos, IEnumerable<Guid> participantesUsuarioIds,
        Guid? organizadorId, CancellationToken ct)
    {
        var ids = participantesUsuarioIds.Append(organizadorId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var pessoas = await db.Usuarios.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Nome, u.Email }).ToListAsync(ct);

        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(Acidente),
            OrigemId = acidente.Id,
            Tipo = TipoAcaoPlano.Corretiva,
            Prioridade = PrioridadeAcao.Alta,
            Descricao = "Realizar a reunião de análise da ocorrência para validar as causas e o plano de ação.",
            Prazo = inicio,
            ResponsavelUsuarioId = organizadorId,
            Fundamentacao = "Reunião de análise · obrigatória em toda ocorrência",
        };
        var ordenadas = pessoas.OrderBy(p => p.Id == organizadorId ? 0 : 1).ThenBy(p => p.Nome).ToList();
        var reuniao = new ReuniaoAnaliseOcorrencia
        {
            AcidenteId = acidente.Id,
            AcaoPlanoId = acao.Id,
            Inicio = inicio,
            Fim = inicio.AddMinutes(duracaoMinutos),
            OrganizadorUsuarioId = organizadorId,
            Participantes = string.Join("\n", ordenadas.Select(p => p.Nome)),
            ParticipantesUsuarioIds = string.Join("\n", ordenadas.Select(p => p.Id)),
        };
        db.AcoesPlano.Add(acao);
        db.ReunioesAnaliseOcorrencia.Add(reuniao);
        return (reuniao, pessoas.Select(p => p.Email).ToList());
    }

    // E-mails dos convidados de uma reunião já gravada (reenvio ao Teams). Reuniões anteriores a
    // 23/10 não guardam os ids: o convite sai só para o organizador.
    public static async Task<List<string>> EmailsConvidadosAsync(IAppDbContext db, ReuniaoAnaliseOcorrencia reuniao, CancellationToken ct)
    {
        var ids = (reuniao.ParticipantesUsuarioIds ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : Guid.Empty)
            .Append(reuniao.OrganizadorUsuarioId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        return await db.Usuarios.Where(u => ids.Contains(u.Id)).Select(u => u.Email).ToListAsync(ct);
    }

    public static async Task MarcarNoTeamsAsync(
        IAppDbContext db, IReuniaoTeamsService? reuniaoTeams, Acidente acidente, ReuniaoAnaliseOcorrencia reuniao,
        List<string> emails, CancellationToken ct)
    {
        reuniao.MotivoFalha = null;
        var tipo = TipoOcorrenciaRotulo.Descrever(acidente.Tipo);
        if (reuniao.OrganizadorUsuarioId is null)
            reuniao.MotivoFalha = "Quem agendou não está vinculado a uma conta Microsoft. Marque a reunião no Teams manualmente.";
        else if (reuniaoTeams is null || !reuniaoTeams.Configurado)
            reuniao.MotivoFalha = "Integração com o Teams não configurada neste ambiente. Marque a reunião no Teams manualmente.";
        else
        {
            try
            {
                var criada = await reuniaoTeams.CriarReuniaoAsync(
                    reuniao.OrganizadorUsuarioId.Value,
                    $"Análise de {tipo.ToLowerInvariant()} · {acidente.Local} · {acidente.Data:dd/MM}",
                    $"Reunião de análise da ocorrência ({tipo.ToLowerInvariant()}) registrada em {acidente.Data:dd/MM/yyyy} ({acidente.Local}).\n\n" +
                    $"Pauta: validar as causas e o plano de ação registrados no sistema SST.\n\nO que aconteceu: {acidente.Descricao}",
                    reuniao.Inicio, reuniao.Fim, emails, ct);
                reuniao.GraphEventId = criada.GraphEventId;
                reuniao.LinkTeams = criada.LinkTeams;
                reuniao.Situacao = SituacaoReuniaoTeams.Criada;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                reuniao.MotivoFalha = Limitar($"O Teams não aceitou a reunião ({ex.Message}). Tente de novo ou marque-a manualmente.", 500);
            }
        }

        if (reuniao.Situacao != SituacaoReuniaoTeams.Criada)
            reuniao.Situacao = SituacaoReuniaoTeams.NaoCriada;
        await db.SaveChangesAsync(ct);
    }

    public static string Limitar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}

// Mesmos rótulos da tela (lib/api.ts: tipoOcorrenciaLabel).
public static class TipoOcorrenciaRotulo
{
    public static string Descrever(TipoOcorrencia tipo) => tipo switch
    {
        TipoOcorrencia.Acidente => "Acidente",
        TipoOcorrencia.Incidente => "Incidente",
        TipoOcorrencia.QuaseAcidente => "Quase acidente",
        TipoOcorrencia.CondicaoInsegura => "Condição insegura",
        TipoOcorrencia.AtoInseguro => "Ato inseguro",
        TipoOcorrencia.DoencaOcupacional => "Doença ocupacional",
        _ => tipo.ToString(),
    };
}
