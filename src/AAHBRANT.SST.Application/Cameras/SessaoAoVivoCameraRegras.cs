using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras;

// Regras e limpeza das sessões de câmera ao vivo. Tudo em SQL (a API tem várias réplicas): quem toca nas sessões (iniciar,
// buscar quadro, enviar quadro, configuração do agente) encerra as paradas antes de agir, e o Worker faz o mesmo de tempos
// em tempos para as que ninguém mais toca.
public static class SessaoAoVivoCameraRegras
{
    // Motivos gravados em MotivoEncerramento.
    public const string MotivoInatividade = "inatividade";
    public const string MotivoTempoMaximo = "tempo_maximo";
    public const string MotivoEncerrada = "encerrada";
    public const string MotivoAgenteSemResposta = "agente_sem_resposta";
    public const string MotivoFalha = "falha";
    public const string MotivoCameraDesabilitada = "camera_desabilitada";
    public const string MotivoAgenteRevogado = "agente_revogado";
    // Mais sessões abertas no agente do que o limite (cliques simultâneos furaram a contagem do Iniciar): a mais nova perde.
    public const string MotivoLimiteSessoes = "limite_sessoes";

    // Códigos de recusa ao abrir a sessão (409) e ao exceder o limite (429).
    public const string CodigoNaoECameraIp = "nao_e_camera_ip";
    public const string CodigoCameraDesabilitada = "camera_desabilitada";
    public const string CodigoAgenteRevogado = "agente_revogado";
    public const string CodigoAgenteSemContato = "agente_sem_contato";
    public const string CodigoAgenteDesatualizado = "agente_desatualizado";
    public const string CodigoLimiteSessoes = "limite_sessoes";

    // Primeira versão do serviço de câmeras que sabe transmitir ao vivo.
    public static readonly Version VersaoMinimaDoAgente = new(1, 1, 0);

    // O agente informa a versão do assembly em 3 partes ("1.1.4"). Ausente ou ilegível = desatualizado.
    public static bool AgenteSuportaAoVivo(string? versao)
    {
        if (string.IsNullOrWhiteSpace(versao)) return false;
        var texto = versao.Trim().TrimStart('v', 'V');
        // "1.1.4-beta" ou "1.1.4+abc": só vale o número.
        var corte = texto.IndexOfAny(new[] { '-', '+' });
        if (corte >= 0) texto = texto[..corte];
        return Version.TryParse(texto, out var v) && v >= VersaoMinimaDoAgente;
    }

    // O que a tela recebe quando a sessão acabou: câmera desligada e agente revogado viram "encerrada".
    public static string MotivoParaTela(string? motivo, string? codigoFalha) => motivo switch
    {
        MotivoInatividade => MotivoInatividade,
        MotivoTempoMaximo => MotivoTempoMaximo,
        MotivoAgenteSemResposta => MotivoAgenteSemResposta,
        MotivoLimiteSessoes => MotivoLimiteSessoes,
        MotivoFalha => $"falha:{CodigosTesteConexaoCamera.Normalizar(false, codigoFalha)}",
        _ => MotivoEncerrada,
    };

    // Motivo pelo qual uma sessão ABERTA já deveria ter acabado (só pelo relógio), ou nulo. Mesma prioridade da limpeza.
    public static string? MotivoDeParada(
        DateTime limiteEmUtc, DateTime ultimaVisualizacaoEmUtc, DateTime solicitadaEmUtc, DateTime? iniciadaEmUtc,
        DateTime? ultimoQuadroRecebidoEmUtc, DateTime agora, AoVivoCameraOpcoes o)
    {
        if (limiteEmUtc <= agora) return MotivoTempoMaximo;
        if (ultimaVisualizacaoEmUtc <= agora.AddSeconds(-o.InatividadeSegundos)) return MotivoInatividade;
        if (iniciadaEmUtc is null && solicitadaEmUtc <= agora.AddSeconds(-o.AgenteSemComecarSegundos)) return MotivoAgenteSemResposta;
        // Já iniciada: o relógio é o do servidor (quando o último quadro chegou); sem ele (ex.: iniciada por falha), vale o início.
        if (iniciadaEmUtc is not null && (ultimoQuadroRecebidoEmUtc ?? iniciadaEmUtc.Value) <= agora.AddSeconds(-o.SegundosSemQuadro)) return MotivoAgenteSemResposta;
        return null;
    }

    public static int SegundosRestantes(DateTime limiteEmUtc, DateTime agora)
        => (int)Math.Max(0, Math.Ceiling((limiteEmUtc - agora).TotalSeconds));

    // Teto de quadros recebidos: fps x 60 x 1,2 por minuto, aplicado como média desde o primeiro quadro (o contador fica no
    // SQL, não na memória da réplica) mais uma folga de poucos segundos para a partida do ffmpeg.
    public static bool ExcedeTetoDeQuadros(long quadrosRecebidos, int fps, DateTime? iniciadaEmUtc, DateTime agora)
    {
        var decorridos = iniciadaEmUtc is { } i ? Math.Max(0, (agora - i).TotalSeconds) : 0;
        var permitidos = fps * 1.2 * (decorridos + 5);
        return quadrosRecebidos + 1 > permitidos;
    }

    // Encerra e zera o último quadro das sessões abertas que pararam: tempo máximo, inatividade, agente que não começou ou que parou de enviar quadros,
    // câmera desligada ou excluída, agente revogado. Sem filtro global nem JOIN (também serve à rota anônima do agente):
    // predicados explícitos e consultas separadas. Com agenteId, só as sessões desse agente. Devolve quantas encerrou.
    public static async Task<int> EncerrarParadasAsync(IAppDbContext db, AoVivoCameraOpcoes opcoes, Guid? agenteId, DateTime agora, CancellationToken ct)
    {
        IQueryable<SessaoAoVivoCamera> Abertas() => db.SessoesAoVivoCamera.IgnoreQueryFilters()
            .Where(s => s.Ativo && s.EncerradaEmUtc == null && (agenteId == null || s.AgenteCameraId == agenteId));

        var total = 0;
        total += await EncerrarAsync(Abertas().Where(s => s.LimiteEmUtc <= agora), MotivoTempoMaximo, agora, ct);
        var corteInatividade = agora.AddSeconds(-opcoes.InatividadeSegundos);
        total += await EncerrarAsync(Abertas().Where(s => s.UltimaVisualizacaoEmUtc <= corteInatividade), MotivoInatividade, agora, ct);
        var corteInicio = agora.AddSeconds(-opcoes.AgenteSemComecarSegundos);
        total += await EncerrarAsync(Abertas().Where(s => s.IniciadaEmUtc == null && s.SolicitadaEmUtc <= corteInicio), MotivoAgenteSemResposta, agora, ct);
        // Iniciada, mas o agente parou de enviar quadros (horário do servidor).
        var corteSemQuadro = agora.AddSeconds(-opcoes.SegundosSemQuadro);
        total += await EncerrarAsync(
            Abertas().Where(s => s.IniciadaEmUtc != null && (s.UltimoQuadroRecebidoEmUtc ?? s.IniciadaEmUtc) <= corteSemQuadro),
            MotivoAgenteSemResposta, agora, ct);

        // Câmera desligada/excluída e agente revogado: lê só ids (sem o quadro) e confere cada um no seu próprio cadastro.
        var restantes = await Abertas().Select(s => new { s.Id, s.CameraId, s.AgenteCameraId }).ToListAsync(ct);
        if (restantes.Count == 0) return total;

        var cameraIds = restantes.Select(s => s.CameraId).Distinct().ToList();
        var camerasOk = (await db.Cameras.IgnoreQueryFilters().AsNoTracking()
            .Where(c => cameraIds.Contains(c.Id) && c.Ativo && c.Habilitada).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var agenteIds = restantes.Select(s => s.AgenteCameraId).Distinct().ToList();
        var agentesOk = (await db.AgentesCamera.IgnoreQueryFilters().AsNoTracking()
            .Where(a => agenteIds.Contains(a.Id) && a.Ativo).Select(a => a.Id).ToListAsync(ct)).ToHashSet();

        var porCamera = restantes.Where(s => !camerasOk.Contains(s.CameraId)).Select(s => s.Id).ToList();
        if (porCamera.Count > 0)
            total += await EncerrarAsync(Abertas().Where(s => porCamera.Contains(s.Id)), MotivoCameraDesabilitada, agora, ct);
        var porAgente = restantes.Where(s => camerasOk.Contains(s.CameraId) && !agentesOk.Contains(s.AgenteCameraId)).Select(s => s.Id).ToList();
        if (porAgente.Count > 0)
            total += await EncerrarAsync(Abertas().Where(s => porAgente.Contains(s.Id)), MotivoAgenteRevogado, agora, ct);
        return total;
    }

    // Fecha as sessões da consulta (só as ainda abertas) e apaga o quadro. Não confere RowVersion de propósito: o fechamento
    // vale mesmo se outra réplica gravou um quadro no mesmo instante.
    public static Task<int> EncerrarAsync(
        IQueryable<SessaoAoVivoCamera> sessoes, string motivo, DateTime agora, CancellationToken ct, string? codigoFalha = null)
        => sessoes.Where(s => s.EncerradaEmUtc == null).ExecuteUpdateAsync(set => set
            .SetProperty(s => s.EncerradaEmUtc, agora)
            .SetProperty(s => s.MotivoEncerramento, motivo)
            .SetProperty(s => s.CodigoFalha, codigoFalha)
            .SetProperty(s => s.UltimoQuadro, (byte[]?)null)
            .SetProperty(s => s.UpdatedAtUtc, agora), ct);
}
