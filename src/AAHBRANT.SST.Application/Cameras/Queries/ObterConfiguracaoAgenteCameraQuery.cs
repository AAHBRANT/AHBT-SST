using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Queries;

// ATENÇÃO: este é o ÚNICO ponto do sistema que devolve a senha de um equipamento (decifrada), e só ao próprio agente da
// obra, autenticado pelo segredo, em HTTPS. O front nunca recebe a senha. Por isso os ToString dos DTOs a omitem.
public record CameraDoAgenteDto(
    Guid CameraId,
    string Nome,
    Guid EquipamentoId,
    string Host,
    int PortaRtsp,
    int PortaOnvif,
    string? EnderecoMac,
    string? Usuario,
    string? Senha,
    int Canal,
    int Stream,
    int IntervaloEnvioSegundos,
    // Metade da imagem que o agente recorta (ffmpeg) antes de analisar/transmitir. Agente antigo ignora e envia a imagem inteira.
    ParteImagemCamera ParteImagem = ParteImagemCamera.Inteira)
{
    public override string ToString() => $"{nameof(CameraDoAgenteDto)} {{ CameraId = {CameraId}, Host = {Host} }}";
}

// Pedido de "Testar conexão" que o agente deve executar agora e devolver por RegistrarResultadoTesteConexaoCameraCommand.
public record TesteConexaoDoAgenteDto(
    Guid TesteId,
    Guid EquipamentoId,
    Guid? CameraId,
    string Host,
    int PortaRtsp,
    int PortaOnvif,
    string? EnderecoMac,
    string? Usuario,
    string? Senha,
    int Canal,
    int Stream,
    ParteImagemCamera ParteImagem = ParteImagemCamera.Inteira)
{
    public override string ToString() => $"{nameof(TesteConexaoDoAgenteDto)} {{ TesteId = {TesteId}, Host = {Host} }}";
}

// Câmera que o agente deve transmitir "quase ao vivo" agora (segundo ffmpeg no stream 1). SegundosRestantes é tempo RELATIVO
// (o relógio do PC da obra pode estar errado): o agente conta no próprio relógio monotônico e para ao chegar a zero.
public record AoVivoDoAgenteDto(Guid SessaoId, Guid CameraId, int Fps, int LarguraMaxima, int SegundosRestantes);

public record ConfiguracaoAgenteCameraDto(
    DateTime GeradaEmUtc,
    // A consulta da configuração é o sinal de vida do agente: consultar nesta cadência (a tela mostra "Agente sem contato" depois de 3 min).
    int IntervaloConsultaSegundos,
    List<CameraDoAgenteDto> Cameras,
    List<TesteConexaoDoAgenteDto> TestesConexao,
    // Agente anterior à 1.1.0 ignora este campo.
    List<AoVivoDoAgenteDto>? AoVivo = null);

// Rota anônima (autenticada pelo segredo do agente): devolve nulo quando o agente não se autentica (o controller responde 401).
// As consultas NÃO dependem do filtro global de RBAC/soft delete (que exige usuário logado e escondia tudo nesta rota só em
// produção): usam IgnoreQueryFilters com os predicados explícitos (Ativo, obra e agente) e sem JOIN por navegação.
public record ObterConfiguracaoAgenteCameraQuery(Guid AgenteId, string? Segredo, string? VersaoAgente) : IRequest<ConfiguracaoAgenteCameraDto?>;

public class ObterConfiguracaoAgenteCameraQueryHandler : IRequestHandler<ObterConfiguracaoAgenteCameraQuery, ConfiguracaoAgenteCameraDto?>
{
    // 15 s (o mínimo que o agente aceita): é também o tempo até o agente ver um pedido de "Testar conexão". Custo: 4 consultas/min
    // por agente, dentro dos limites de 600/min por agente e 900/min por IP (quadros somam até ~180/min); o contato do agente
    // continua gravado no máximo a cada 30 s (ContatoAgenteCamera), bem abaixo dos 3 min do "Agente sem contato".
    public const int IntervaloConsultaSegundos = 15;

    private readonly IAppDbContext _db;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly ICredencialCameraCriptografia _cripto;
    private readonly ILogger<ObterConfiguracaoAgenteCameraQueryHandler> _logger;
    private readonly AoVivoCameraOpcoes _opcoes;

    public ObterConfiguracaoAgenteCameraQueryHandler(
        IAppDbContext db,
        IAgenteCameraAutenticador autenticador,
        ICurrentUserService usuarioAtual,
        ICredencialCameraCriptografia cripto,
        ILogger<ObterConfiguracaoAgenteCameraQueryHandler> logger,
        IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _autenticador = autenticador;
        _usuarioAtual = usuarioAtual;
        _cripto = cripto;
        _logger = logger;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public async Task<ConfiguracaoAgenteCameraDto?> Handle(ObterConfiguracaoAgenteCameraQuery request, CancellationToken ct)
    {
        var agente = await _autenticador.ValidarAsync(request.AgenteId, request.Segredo, ct);
        if (agente is null) return null;

        // Mesma regra do envio de quadro: só depois do segredo validado o escopo passa a ser a obra do agente.
        _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { agente.ObraId });

        await ContatoAgenteCamera.RegistrarAsync(_db, agente, request.VersaoAgente, _logger, ct);

        var obraId = agente.ObraId;
        var agenteId = agente.Id;

        // Limpeza oportunista das miniaturas de teste esquecidas: nunca impede o agente de receber a configuração.
        try
        {
            await MiniaturasTesteConexao.LimparAntigasAsync(_db, agenteId, DateTime.UtcNow, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Limpeza de miniaturas de teste do agente {AgenteId} não concluída.", agenteId);
            _db.DescartarAlteracoesPendentes();
        }

        var equipamentos = await _db.EquipamentosCamera.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Ativo && e.ObraId == obraId && e.AgenteCameraId == agenteId)
            .ToDictionaryAsync(e => e.Id, ct);
        var equipamentoIds = equipamentos.Keys.ToList();

        var cameras = await _db.Cameras.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Ativo && c.Habilitada && c.Fonte == FonteCamera.Rtsp && c.ObraId == obraId
                && c.EquipamentoCameraId != null && equipamentoIds.Contains(c.EquipamentoCameraId.Value))
            .OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome, c.EquipamentoCameraId, c.Canal, c.Stream, c.IntervaloEnvioSegundos, c.ParteImagem })
            .ToListAsync(ct);

        var senhas = new Dictionary<Guid, string?>();
        string? Senha(Guid equipamentoId)
        {
            if (senhas.TryGetValue(equipamentoId, out var cache)) return cache;
            var cifrada = equipamentos[equipamentoId].SenhaCifrada;
            return senhas[equipamentoId] = string.IsNullOrEmpty(cifrada) ? null : _cripto.Decifrar(cifrada);
        }

        var configuradas = new List<CameraDoAgenteDto>();
        foreach (var c in cameras)
        {
            var equipamento = equipamentos[c.EquipamentoCameraId!.Value];
            // Câmera IP só funciona com senha definida (regra do produto): sem ela, ou com senha que não decifra, não vai ao agente.
            var senha = Senha(equipamento.Id);
            if (senha is null)
            {
                _logger.LogWarning("Câmera {CameraId} fora da configuração do agente {AgenteId}: equipamento sem senha utilizável.", c.Id, agenteId);
                continue;
            }

            configuradas.Add(new CameraDoAgenteDto(
                c.Id, c.Nome, equipamento.Id, equipamento.Host, equipamento.PortaRtsp, equipamento.PortaOnvif, equipamento.EnderecoMac,
                equipamento.Usuario, senha, c.Canal ?? 1, c.Stream ?? 0, c.IntervaloEnvioSegundos, c.ParteImagem));
        }

        var testes = await EntregarTestesPendentesAsync(agenteId, obraId, equipamentos, Senha, ct);

        var aoVivo = await ListarAoVivoAsync(agenteId, configuradas, ct);

        return new ConfiguracaoAgenteCameraDto(DateTime.UtcNow, IntervaloConsultaSegundos, configuradas, testes, aoVivo);
    }

    // Sessões ao vivo abertas deste agente, só das câmeras que já estão em "cameras" (as demais o agente nem conhece). Sem filtro
    // global nem JOIN: predicados explícitos por agente. Encerra antes as que pararam. Falha aqui nunca impede a configuração.
    private async Task<List<AoVivoDoAgenteDto>> ListarAoVivoAsync(Guid agenteId, List<CameraDoAgenteDto> configuradas, CancellationToken ct)
    {
        try
        {
            var agora = DateTime.UtcNow;
            await SessaoAoVivoCameraRegras.EncerrarParadasAsync(_db, _opcoes, agenteId, agora, ct);

            var cameraIds = configuradas.Select(c => c.CameraId).ToList();
            var abertas = await _db.SessoesAoVivoCamera.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Ativo && s.AgenteCameraId == agenteId && s.EncerradaEmUtc == null && s.LimiteEmUtc > agora)
                .OrderBy(s => s.SolicitadaEmUtc).ThenBy(s => s.Id)
                .Select(s => new { s.Id, s.CameraId, s.Fps, s.LimiteEmUtc })
                .ToListAsync(ct);

            // O Iniciar conta e insere sem atomicidade: cliques simultâneos em câmeras diferentes podem passar do limite.
            // Aqui o servidor entrega só as MaximoCamerasPorAgente mais antigas e encerra as excedentes (a tela recebe
            // 410 { motivo: "limite_sessoes" } na próxima leitura do quadro).
            var maximo = Math.Max(0, _opcoes.MaximoCamerasPorAgente);
            if (abertas.Count > maximo)
            {
                var excedentes = abertas.Skip(maximo).Select(s => s.Id).ToList();
                await SessaoAoVivoCameraRegras.EncerrarAsync(
                    _db.SessoesAoVivoCamera.IgnoreQueryFilters().Where(s => s.Ativo && s.AgenteCameraId == agenteId && excedentes.Contains(s.Id)),
                    SessaoAoVivoCameraRegras.MotivoLimiteSessoes, agora, ct);
                abertas = abertas.Take(maximo).ToList();
            }

            var sessoes = abertas.Where(s => cameraIds.Contains(s.CameraId)).ToList();
            return sessoes
                .Select(s => new AoVivoDoAgenteDto(s.Id, s.CameraId, s.Fps, _opcoes.LarguraMaxima, SessaoAoVivoCameraRegras.SegundosRestantes(s.LimiteEmUtc, agora)))
                .ToList();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Sessões ao vivo do agente {AgenteId} não listadas.", agenteId);
            _db.DescartarAlteracoesPendentes();
            return new List<AoVivoDoAgenteDto>();
        }
    }

    private async Task<List<TesteConexaoDoAgenteDto>> EntregarTestesPendentesAsync(
        Guid agenteId, Guid obraId, Dictionary<Guid, Domain.Entidades.EquipamentoCamera> equipamentos, Func<Guid, string?> senhaDe, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var limite = agora - SolicitarTesteConexaoCameraCommandHandler.ValidadeDoPedido;
        var pendentes = await _db.TestesConexaoCamera.IgnoreQueryFilters()
            .Where(t => t.Ativo && t.ObraId == obraId && t.AgenteCameraId == agenteId && t.ConcluidoEmUtc == null && t.SolicitadoEmUtc >= limite)
            .OrderBy(t => t.SolicitadoEmUtc)
            .ToListAsync(ct);

        var entregues = new List<TesteConexaoDoAgenteDto>();
        foreach (var t in pendentes)
        {
            // Equipamento excluído ou movido para outro agente: o pedido fica sem resposta e expira.
            if (!equipamentos.TryGetValue(t.EquipamentoCameraId, out var e)) continue;

            t.EntregueAoAgenteEmUtc ??= agora;
            entregues.Add(new TesteConexaoDoAgenteDto(
                t.Id, e.Id, t.CameraId, e.Host, e.PortaRtsp, e.PortaOnvif, e.EnderecoMac, e.Usuario, senhaDe(e.Id), t.Canal, t.Stream, t.ParteImagem));
        }

        // Só anotação informativa: falhar aqui não pode impedir o agente de receber a configuração.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Entrega de testes de conexão ao agente {AgenteId} não anotada.", agenteId);
            _db.DescartarAlteracoesPendentes();
        }

        return entregues;
    }
}
