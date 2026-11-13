using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// PermitirQuadroAtrasado: só o agente de câmeras da obra liga (o quadro dele pode ter ficado guardado sem internet).
// Ligado, um quadro com CapturadaEmUtc bem anterior ao recebimento sai marcado com o horário REAL da captura e não
// disputa agrupamento nem cota de alerta com eventos ao vivo. Desligado (navegador), nada muda.
public record AnalisarQuadroCameraCommand(Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc, bool PermitirQuadroAtrasado = false)
    : IRequest<ResultadoQuadroDto>;

// TrabalhadorId/TrabalhadorNome: quem o reconhecimento facial identificou na ocorrência (nulo = ninguém).
// Repetida: nenhuma ocorrência nova foi aberta; a irregularidade foi somada a ocorrências já existentes.
public record ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete,
    Guid? TrabalhadorId = null, string? TrabalhadorNome = null, bool Repetida = false);

public static class SituacaoQuadro
{
    public const string Ignorado = "ignorado";     // envio duplicado ou em paralelo; nada foi analisado
    public const string Limpo = "limpo";           // analisado, sem irregularidade; foto descartada
    public const string Ocorrencia = "ocorrencia"; // irregularidade gravada
    public const string Falha = "falha";           // a IA não conseguiu analisar; nada gravado
}

public class AnalisarQuadroCameraCommandValidator : AbstractValidator<AnalisarQuadroCameraCommand>
{
    public const int TamanhoMaximoBytes = 4_000_000;

    public AnalisarQuadroCameraCommandValidator()
    {
        RuleFor(x => x.CameraId).NotEmpty();
        RuleFor(x => x.Imagem)
            .NotEmpty()
            .Must(i => i.Length <= TamanhoMaximoBytes).WithMessage("A imagem excede o tamanho máximo de 4 MB.")
            .Must(i => i.Length >= 2 && i[0] == 0xFF && i[1] == 0xD8).WithMessage("A imagem precisa estar em JPEG.");
    }
}

public class AnalisarQuadroCameraCommandHandler : IRequestHandler<AnalisarQuadroCameraCommand, ResultadoQuadroDto>
{
    // Sem intervalo entre análises (decisão do usuário em 09/11/2026): o navegador envia um quadro após o outro
    // enquanto houver movimento. Este mínimo só barra envio duplicado ou em paralelo da mesma câmera.
    public static readonly TimeSpan IntervaloMinimoEntreAnalises = TimeSpan.FromSeconds(1);

    // Depois de alertar sobre um trabalhador identificado, só volta a alertar sobre ele passado este tempo.
    public static readonly TimeSpan IntervaloAlertasPorTrabalhador = TimeSpan.FromHours(1);

    // A identificação pelo rosto não pode segurar o alerta: passou disso, sai como "não identificado".
    public static readonly TimeSpan TempoMaximoIdentificacaoFacial = TimeSpan.FromSeconds(20);

    // Repetição da mesma irregularidade (a análise roda sem parar). O mesmo trabalhador na mesma câmera
    // soma na ocorrência aberta há menos de uma hora. Sem identificação não dá para saber se é a mesma
    // pessoa: soma enquanto a cena continuar irregular (visto de novo em até 5 minutos), por no máximo uma hora.
    public static readonly TimeSpan DuracaoMaximaDaOcorrencia = TimeSpan.FromHours(1);
    public static readonly TimeSpan PausaQueEncerraSemIdentificacao = TimeSpan.FromMinutes(5);

    // Quadro do agente recebido com mais atraso que isto (captura x recebimento) é "atrasado": usa o horário da captura.
    public static readonly TimeSpan ToleranciaQuadroAtrasado = TimeSpan.FromMinutes(2);

    private readonly IAppDbContext _db;
    private readonly IAnalisadorEpiImagem _analisador;
    private readonly ITelegramResumoService _telegram;
    private readonly IAutenticacaoFacialService _facial;
    private readonly ILogger<AnalisarQuadroCameraCommandHandler> _logger;

    public AnalisarQuadroCameraCommandHandler(
        IAppDbContext db,
        IAnalisadorEpiImagem analisador,
        ITelegramResumoService telegram,
        IAutenticacaoFacialService facial,
        ILogger<AnalisarQuadroCameraCommandHandler> logger)
    {
        _db = db;
        _analisador = analisador;
        _telegram = telegram;
        _facial = facial;
        _logger = logger;
    }

    private sealed record Identificado(Guid Id, string Nome, double? Confianca);

    public async Task<ResultadoQuadroDto> Handle(AnalisarQuadroCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.Include(c => c.Obra).FirstOrDefaultAsync(c => c.Id == request.CameraId, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.CameraId} não encontrada.");

        if (!camera.Habilitada)
            throw new InvalidOperationException("Esta câmera está com o monitoramento desligado.");

        var agora = DateTime.UtcNow;
        if (camera.UltimaAnaliseEmUtc is { } ultima && agora - ultima < IntervaloMinimoEntreAnalises)
            return new ResultadoQuadroDto(SituacaoQuadro.Ignorado, null, false, 0, 0, 0);

        // Gravado antes de chamar a IA: um segundo envio em paralelo já encontra o teto aplicado.
        // Também conta a análise. Dois quadros simultâneos da mesma câmera disputam a versão da linha:
        // o perdedor é tratado como "ignorado", não como erro.
        camera.UltimaAnaliseEmUtc = agora;
        camera.TotalAnalises++;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoQuadroDto(SituacaoQuadro.Ignorado, null, false, 0, 0, 0);
        }

        ResultadoAnaliseEpi analise;
        try
        {
            analise = await _analisador.AnalisarAsync(request.Imagem, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Análise de EPI da câmera {CameraId} não concluída.", camera.Id);
            return new ResultadoQuadroDto(SituacaoQuadro.Falha, null, false, 0, 0, 0);
        }

        if (analise.SemCapacete == 0 && analise.SemColete == 0)
        {
            await FinalizarCameraAsync(camera, analise.Uso, agora, alertaEnviadoEm: null);
            return new ResultadoQuadroDto(SituacaoQuadro.Limpo, null, false, analise.Pessoas, 0, 0);
        }

        // Várias pessoas na cena, com a posição de cada uma: identifica cada irregular pelo rosto.
        if (analise.Pessoas > 1 && analise.Detalhe is { Count: > 1 } detalhe && detalhe.Any(p => p.Irregular))
            return await RegistrarCenaComVariasPessoasAsync(camera, request, analise, detalhe, agora);

        // Uma única pessoa na cena: o rosto da foto é o dela. (Várias pessoas sem posição conhecida ficam
        // sem identificação.) Vem antes de gravar, para a repetição do mesmo trabalhador somar na ocorrência dele.
        var identificado = analise.Pessoas == 1 ? await IdentificarPeloRostoAsync(camera, request.Imagem) : null;

        var (ocorrencia, nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise, analise.SemCapacete, analise.SemColete,
            identificado?.Id, identificado?.Confianca, agora);

        // A ocorrência é gravada ANTES do alerta e sem o token da requisição: a chamada à IA pode demorar
        // minutos e um cancelamento do cliente ou conflito na câmera não pode perder a evidência.
        await _db.SaveChangesAsync(CancellationToken.None);

        // Cada ocorrência alerta no máximo uma vez. Trabalhador identificado: um alerta por pessoa a cada
        // hora, em qualquer câmera (regra do usuário em 09/11/2026), no lugar do intervalo da câmera, para o
        // alerta de um não segurar o de outro. Sem identificação: vale o intervalo da câmera.
        var atrasadoEm = AtrasoDoQuadro(request, agora);
        var podeAlertar = !ocorrencia.AlertaEnviado
            && (identificado is not null
                ? !(await NotificadosNaUltimaHoraAsync(new[] { identificado.Id }, agora, atrasadoEm)).Contains(identificado.Id)
                : await IntervaloDaCameraPermiteAsync(camera, agora, atrasadoEm));

        DateTime? enviadoEm = null;
        if (podeAlertar)
        {
            var legenda = LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome, atrasadoEm ?? agora,
                ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete, identificado?.Nome)
                + LegendaAlertaEpi.AvisoDeAtraso(atrasadoEm);
            if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
            {
                enviadoEm = DateTime.UtcNow;
                await MarcarAlertaEnviadoAsync(new[] { ocorrencia }, enviadoEm.Value);
            }
        }

        // Alerta de quadro atrasado não ocupa o intervalo entre alertas da câmera: esse intervalo é dos eventos ao vivo.
        await FinalizarCameraAsync(camera, analise.Uso, agora, atrasadoEm is null ? enviadoEm : null);

        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, ocorrencia.Id, enviadoEm is not null,
            analise.Pessoas, analise.SemCapacete, analise.SemColete, identificado?.Id, identificado?.Nome, Repetida: !nova);
    }

    // Cena com várias pessoas. Cada irregular identificado pelo rosto tem a própria ocorrência (com o que
    // faltava nele), para a conferência e a regra de um alerta por trabalhador por hora valerem por pessoa.
    // Os irregulares sem identificação ficam juntos numa ocorrência sem trabalhador. Sai um único alerta
    // por foto, com os nomes de quem ainda não foi notificado na última hora.
    private async Task<ResultadoQuadroDto> RegistrarCenaComVariasPessoasAsync(
        Camera camera, AnalisarQuadroCameraCommand request, ResultadoAnaliseEpi analise, IReadOnlyList<PessoaNaCena> detalhe, DateTime agora)
    {
        var rostos = await IdentificarRostosDaCenaAsync(camera, request.Imagem);
        var rostoPorPessoa = CruzamentoRostosPessoas.Cruzar(detalhe, rostos);
        _logger.LogInformation(
            "Cena da câmera {CameraId}: {Pessoas} pessoas ({Irregulares} irregulares), {Rostos} rostos na foto, {Reconhecidos} reconhecidos, {Ligados} ligados a uma pessoa.",
            camera.Id, detalhe.Count, detalhe.Count(p => p.Irregular), rostos.Count, rostos.Count(r => r.TrabalhadorId is not null), rostoPorPessoa.Count);

        var ids = rostoPorPessoa.Values.Select(r => r.TrabalhadorId!.Value).Distinct().ToList();
        var nomes = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Trabalhadores.Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Nome })
                .ToDictionaryAsync(t => t.Id, t => t.Nome, CancellationToken.None);

        var identificadas = new List<(OcorrenciaEpiCamera Ocorrencia, IdentificadoNaCena Pessoa)>();
        var naoIdentificados = new List<PessoaNaCena>();
        var jaIncluidos = new HashSet<Guid>();
        var algumaNova = false;
        for (var i = 0; i < detalhe.Count; i++)
        {
            var pessoa = detalhe[i];
            if (!pessoa.Irregular) continue;

            // O mesmo trabalhador não entra duas vezes na mesma foto, nem um que o banco não enxerga.
            if (rostoPorPessoa.TryGetValue(i, out var rosto) && rosto.TrabalhadorId is { } trabalhadorId
                && nomes.TryGetValue(trabalhadorId, out var nome) && jaIncluidos.Add(trabalhadorId))
            {
                var (ocorrencia, nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise,
                    pessoa.SemCapacete ? 1 : 0, pessoa.SemColete ? 1 : 0, trabalhadorId, rosto.Confianca, agora);
                algumaNova |= nova;
                identificadas.Add((ocorrencia, new IdentificadoNaCena(nome, pessoa.SemCapacete, pessoa.SemColete)));
            }
            else
            {
                naoIdentificados.Add(pessoa);
            }
        }

        OcorrenciaEpiCamera? semIdentificacao = null;
        if (naoIdentificados.Count > 0)
        {
            (semIdentificacao, var nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise,
                naoIdentificados.Count(p => p.SemCapacete), naoIdentificados.Count(p => p.SemColete), null, null, agora);
            algumaNova |= nova;
        }

        // Gravadas antes do alerta e sem o token da requisição, como na cena de uma pessoa só.
        await _db.SaveChangesAsync(CancellationToken.None);

        // Quem já foi notificado na última hora (em qualquer câmera) fica fora do alerta desta foto.
        var atrasadoEm = AtrasoDoQuadro(request, agora);
        var notificadosNaHora = await NotificadosNaUltimaHoraAsync(ids, agora, atrasadoEm);
        var aAlertar = identificadas
            .Where(i => !i.Ocorrencia.AlertaEnviado && !notificadosNaHora.Contains(i.Ocorrencia.TrabalhadorId!.Value))
            .ToList();

        // Sem identificação vale o intervalo da câmera, como na cena de uma pessoa só.
        var alertaSemIdentificacao = semIdentificacao is { AlertaEnviado: false } && await IntervaloDaCameraPermiteAsync(camera, agora, atrasadoEm);

        var totalSemCapacete = detalhe.Count(p => p.SemCapacete);
        var totalSemColete = detalhe.Count(p => p.SemColete);
        DateTime? enviadoEm = null;
        if (aAlertar.Count > 0 || alertaSemIdentificacao)
        {
            var legenda = LegendaAlertaEpi.MontarCena(camera.Obra?.Nome ?? "-", camera.Nome, atrasadoEm ?? agora, analise.Pessoas,
                totalSemCapacete, totalSemColete, aAlertar.Select(i => i.Pessoa).ToList(),
                alertaSemIdentificacao ? naoIdentificados.Count : 0)
                + LegendaAlertaEpi.AvisoDeAtraso(atrasadoEm);
            if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
            {
                enviadoEm = DateTime.UtcNow;
                var alertadas = aAlertar.Select(i => i.Ocorrencia).ToList();
                if (alertaSemIdentificacao) alertadas.Add(semIdentificacao!);
                await MarcarAlertaEnviadoAsync(alertadas, enviadoEm.Value);
            }
        }

        await FinalizarCameraAsync(camera, analise.Uso, agora, atrasadoEm is null ? enviadoEm : null);

        var primeira = identificadas.Select(i => i.Ocorrencia).FirstOrDefault() ?? semIdentificacao!;
        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, primeira.Id, enviadoEm is not null,
            analise.Pessoas, totalSemCapacete, totalSemColete,
            identificadas.Count == 1 ? identificadas[0].Ocorrencia.TrabalhadorId : null,
            identificadas.Count == 0 ? null : string.Join(", ", identificadas.Select(i => i.Pessoa.Nome)),
            Repetida: !algumaNova);
    }

    // Soma a irregularidade numa ocorrência já aberta (mesma câmera e mesmo trabalhador, ou a mesma cena sem
    // identificação) ou abre uma nova. Na repetição a foto e o horário continuam os da primeira vez; contam
    // as vezes vistas, a última vez e o pior caso das contagens. Não grava: quem chama salva.
    private async Task<(OcorrenciaEpiCamera Ocorrencia, bool Nova)> ObterOuCriarOcorrenciaAsync(
        Camera camera, AnalisarQuadroCameraCommand request, ResultadoAnaliseEpi analise, int semCapacete, int semColete,
        Guid? trabalhadorId, double? confiancaIdentificacao, DateTime agora)
    {
        // Agente: as janelas contam do instante REAL do quadro (captura, se atrasado) e olham o horário de captura das
        // ocorrências, para um quadro atrasado só agrupar com eventos da mesma época e um evento ao vivo não cair numa
        // ocorrência antiga gravada agora. No navegador (flag desligada) segue tudo como antes.
        var referencia = AtrasoDoQuadro(request, agora) ?? agora;
        var abertaDesde = referencia - DuracaoMaximaDaOcorrencia;
        var vistaDesde = referencia - PausaQueEncerraSemIdentificacao;
        var candidatas = _db.OcorrenciasEpiCamera.Where(o => o.CameraId == camera.Id && o.RecebidaEmUtc >= abertaDesde);
        if (request.PermitirQuadroAtrasado)
        {
            var limiteSuperior = referencia + (trabalhadorId is null ? PausaQueEncerraSemIdentificacao : DuracaoMaximaDaOcorrencia);
            candidatas = candidatas.Where(o => o.CapturadaEmUtc >= abertaDesde && o.CapturadaEmUtc <= limiteSuperior);
        }
        candidatas = trabalhadorId is { } id
            ? candidatas.Where(o => o.TrabalhadorId == id)
            : candidatas.Where(o => o.TrabalhadorId == null && (o.UltimaVezEmUtc ?? o.RecebidaEmUtc) >= vistaDesde);

        var existente = await candidatas.OrderByDescending(o => o.RecebidaEmUtc).FirstOrDefaultAsync(CancellationToken.None);
        if (existente is not null)
        {
            existente.VezesVista++;
            if (existente.UltimaVezEmUtc is not { } ultimaVez || referencia > ultimaVez) existente.UltimaVezEmUtc = referencia;
            existente.PessoasNaCena = Math.Max(existente.PessoasNaCena, analise.Pessoas);
            existente.SemCapacete = Math.Max(existente.SemCapacete, semCapacete);
            existente.SemColete = Math.Max(existente.SemColete, semColete);
            if (existente.IdentificadoPorRosto && confiancaIdentificacao is { } confianca
                && confianca > (existente.ConfiancaIdentificacao ?? 0))
                existente.ConfiancaIdentificacao = confianca;
            return (existente, false);
        }

        var nova = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id,
            ObraId = camera.ObraId,
            CapturadaEmUtc = request.CapturadaEmUtc,
            RecebidaEmUtc = agora,
            UltimaVezEmUtc = referencia,
            Foto = request.Imagem,
            PessoasNaCena = analise.Pessoas,
            SemCapacete = semCapacete,
            SemColete = semColete,
            Confianca = analise.Confianca,
        };
        if (trabalhadorId is not null)
        {
            nova.TrabalhadorId = trabalhadorId;
            nova.IdentificadoPorRosto = true;
            nova.ConfiancaIdentificacao = confiancaIdentificacao;
            nova.VinculadoEmUtc = agora;
        }
        _db.OcorrenciasEpiCamera.Add(nova);
        return (nova, true);
    }

    // Trabalhadores (entre os informados) que já receberam alerta na última hora, em qualquer câmera.
    // A hora conta do evento, não do envio: um alerta de quadro atrasado, enviado agora sobre algo de horas atrás, não
    // consome a cota de quem aparece ao vivo (o horário de captura mais velho que a janela o tira da conta). Para o
    // quadro atrasado a pergunta é outra: já houve alerta de um evento dentro de 1 h do horário dele (atrasadoEm).
    private async Task<List<Guid>> NotificadosNaUltimaHoraAsync(IReadOnlyCollection<Guid> trabalhadorIds, DateTime agora, DateTime? atrasadoEm = null)
    {
        if (trabalhadorIds.Count == 0) return new List<Guid>();
        var consulta = _db.OcorrenciasEpiCamera
            .Where(o => o.TrabalhadorId != null && trabalhadorIds.Contains(o.TrabalhadorId.Value) && o.AlertaEnviado);
        if (atrasadoEm is { } quando)
        {
            var de = quando - IntervaloAlertasPorTrabalhador;
            var ate = quando + IntervaloAlertasPorTrabalhador;
            consulta = consulta.Where(o => o.CapturadaEmUtc >= de && o.CapturadaEmUtc <= ate);
        }
        else
        {
            var limite = agora - IntervaloAlertasPorTrabalhador;
            var capturaMinima = limite - ToleranciaQuadroAtrasado;
            consulta = consulta.Where(o => o.AlertaEnviadoEmUtc >= limite && o.CapturadaEmUtc >= capturaMinima);
        }
        return await consulta.Select(o => o.TrabalhadorId!.Value).Distinct().ToListAsync(CancellationToken.None);
    }

    // Horário real da captura quando o quadro (do agente) chegou atrasado; nulo para quadro ao vivo ou do navegador.
    private static DateTime? AtrasoDoQuadro(AnalisarQuadroCameraCommand request, DateTime agora)
        => request.PermitirQuadroAtrasado && request.CapturadaEmUtc < agora - ToleranciaQuadroAtrasado ? request.CapturadaEmUtc : null;

    // Quadro atrasado: o intervalo da câmera conta pelos alertas de eventos próximos do horário DELE (sem identificação),
    // não pelo último alerta enviado (que pode ser de agora e pertencer a outra época).
    private async Task<bool> IntervaloDaCameraPermiteAsync(Camera camera, DateTime agora, DateTime? atrasadoEm)
    {
        if (atrasadoEm is not { } quando) return IntervaloDaCameraPermite(camera, agora);
        if (camera.IntervaloMinimoAlertasMinutos == 0) return true;
        var janela = TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);
        var de = quando - janela;
        var ate = quando + janela;
        return !await _db.OcorrenciasEpiCamera.AnyAsync(
            o => o.CameraId == camera.Id && o.TrabalhadorId == null && o.AlertaEnviado && o.CapturadaEmUtc >= de && o.CapturadaEmUtc <= ate,
            CancellationToken.None);
    }

    // O alerta já saiu no Telegram: perder a marca por conflito de versão (o técnico mexeu na ocorrência neste instante)
    // faria o agente reenviar o quadro e o alerta sair duas vezes. Recarrega a linha, remarca e tenta de novo; se
    // ainda assim não der, só registra (nunca derruba a resposta nem devolve dados da ocorrência).
    private async Task MarcarAlertaEnviadoAsync(IReadOnlyCollection<OcorrenciaEpiCamera> ocorrencias, DateTime enviadoEm)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            foreach (var o in ocorrencias)
            {
                o.AlertaEnviado = true;
                o.AlertaEnviadoEmUtc = enviadoEm;
            }
            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (tentativa >= 3)
                {
                    _logger.LogWarning(ex, "Marca de alerta enviado não gravada após {Tentativas} tentativas (ocorrência alterada em paralelo).", tentativa);
                    return;
                }
                foreach (var entrada in ex.Entries) await entrada.ReloadAsync(CancellationToken.None);
            }
        }
    }

    private static bool IntervaloDaCameraPermite(Camera camera, DateTime agora)
        => camera.IntervaloMinimoAlertasMinutos == 0
           || camera.UltimoAlertaEmUtc is null
           || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);

    // Última gravação da análise, sempre na câmera: consumo real de tokens e, se saiu alerta, o horário dele.
    // Fica por último e isolada porque a câmera pode ter sido editada durante a análise; nesse conflito só se
    // perde o consumo e o intervalo entre alertas desta vez, nunca a ocorrência.
    private async Task FinalizarCameraAsync(Camera camera, UsoIa? uso, DateTime agora, DateTime? alertaEnviadoEm)
    {
        var hojeEmBrasilia = agora.AddHours(-3).Date;
        if (camera.DiaDoConsumo != hojeEmBrasilia)
        {
            camera.DiaDoConsumo = hojeEmBrasilia;
            camera.AnalisesNoDia = 0;
            camera.TokensEntradaNoDia = 0;
            camera.TokensSaidaNoDia = 0;
        }
        camera.AnalisesNoDia++;
        if (uso is not null)
        {
            camera.TotalTokensEntrada += uso.TokensEntrada;
            camera.TotalTokensSaida += uso.TokensSaida;
            camera.TokensEntradaNoDia += uso.TokensEntrada;
            camera.TokensSaidaNoDia += uso.TokensSaida;
        }
        if (alertaEnviadoEm is not null) camera.UltimoAlertaEmUtc = alertaEnviadoEm;

        try
        {
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Câmera {CameraId} alterada durante a análise; consumo e intervalo entre alertas não registrados.", camera.Id);
        }
    }

    // Todos os rostos da foto, já reconhecidos na galeria facial da obra. Falha ou demora: lista vazia, e
    // a cena segue sem identificação.
    private async Task<IReadOnlyList<RostoNaCena>> IdentificarRostosDaCenaAsync(Camera camera, byte[] foto)
    {
        try
        {
            using var limite = new CancellationTokenSource(TempoMaximoIdentificacaoFacial);
            return await _facial.IdentificarRostosDaCenaAsync(camera.ObraId, foto, limite.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identificação dos rostos da cena da câmera {CameraId} não concluída.", camera.Id);
            return Array.Empty<RostoNaCena>();
        }
    }

    // Procura o rosto da foto na galeria facial da obra (a mesma da assinatura). Qualquer recusa ou falha
    // (rosto não reconhecido, obra sem facial, sem consentimento, Azure fora do ar) devolve nulo, e a
    // ocorrência fica sem trabalhador, para o técnico vincular à mão.
    private async Task<Identificado?> IdentificarPeloRostoAsync(Camera camera, byte[] foto)
    {
        try
        {
            using var limite = new CancellationTokenSource(TempoMaximoIdentificacaoFacial);
            var identificacao = await _facial.IdentificarSemRegistrarFalhaAsync(camera.ObraId, foto, limite.Token);
            if (!identificacao.Aceito || identificacao.Resultado is null)
            {
                // Sem isto, "não identificado" não diz por quê (ex.: o mesmo rosto cadastrado em duas pessoas
                // sai como RostoAmbiguo, e parecia defeito do reconhecimento).
                _logger.LogInformation("Rosto na câmera {CameraId} não identificado: {Motivo} (confiança {Confianca}).",
                    camera.Id, identificacao.Motivo, identificacao.Confianca);
                return null;
            }

            var trabalhador = await _db.Trabalhadores
                .Where(t => t.Id == identificacao.Resultado.TrabalhadorId)
                .Select(t => new { t.Id, t.Nome })
                .FirstOrDefaultAsync(CancellationToken.None);
            return trabalhador is null ? null : new Identificado(trabalhador.Id, trabalhador.Nome, identificacao.Resultado.Confianca);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identificação pelo rosto na câmera {CameraId} não concluída.", camera.Id);
            return null;
        }
    }

    // Falha no Telegram não pode impedir o registro: a ocorrência fica gravada com "alerta não enviado".
    private async Task<bool> EnviarLegendaAsync(Camera camera, byte[] foto, string legenda)
    {
        // Câmera sem aviso no Telegram: a análise e a ocorrência seguem iguais; só o envio é pulado (sem marcar "alerta enviado").
        if (!camera.AvisarTelegram) return false;

        try
        {
            return await _telegram.EnviarFotoAsync(foto, legenda, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alerta de EPI da câmera {CameraId} não enviado ao Telegram.", camera.Id);
            return false;
        }
    }
}
