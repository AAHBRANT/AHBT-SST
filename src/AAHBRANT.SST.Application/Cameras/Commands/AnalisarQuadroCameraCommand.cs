using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record AnalisarQuadroCameraCommand(Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc) : IRequest<ResultadoQuadroDto>;

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
        var podeAlertar = !ocorrencia.AlertaEnviado
            && (identificado is not null
                ? !(await NotificadosNaUltimaHoraAsync(new[] { identificado.Id }, agora)).Contains(identificado.Id)
                : IntervaloDaCameraPermite(camera, agora));

        DateTime? enviadoEm = null;
        if (podeAlertar)
        {
            var legenda = LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome, agora,
                ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete, identificado?.Nome);
            if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
            {
                enviadoEm = DateTime.UtcNow;
                ocorrencia.AlertaEnviado = true;
                ocorrencia.AlertaEnviadoEmUtc = enviadoEm;
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }

        await FinalizarCameraAsync(camera, analise.Uso, agora, enviadoEm);

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
        var notificadosNaHora = await NotificadosNaUltimaHoraAsync(ids, agora);
        var aAlertar = identificadas
            .Where(i => !i.Ocorrencia.AlertaEnviado && !notificadosNaHora.Contains(i.Ocorrencia.TrabalhadorId!.Value))
            .ToList();

        // Sem identificação vale o intervalo da câmera, como na cena de uma pessoa só.
        var alertaSemIdentificacao = semIdentificacao is { AlertaEnviado: false } && IntervaloDaCameraPermite(camera, agora);

        var totalSemCapacete = detalhe.Count(p => p.SemCapacete);
        var totalSemColete = detalhe.Count(p => p.SemColete);
        DateTime? enviadoEm = null;
        if (aAlertar.Count > 0 || alertaSemIdentificacao)
        {
            var legenda = LegendaAlertaEpi.MontarCena(camera.Obra?.Nome ?? "-", camera.Nome, agora, analise.Pessoas,
                totalSemCapacete, totalSemColete, aAlertar.Select(i => i.Pessoa).ToList(),
                alertaSemIdentificacao ? naoIdentificados.Count : 0);
            if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
            {
                enviadoEm = DateTime.UtcNow;
                foreach (var (ocorrencia, _) in aAlertar)
                {
                    ocorrencia.AlertaEnviado = true;
                    ocorrencia.AlertaEnviadoEmUtc = enviadoEm;
                }
                if (alertaSemIdentificacao)
                {
                    semIdentificacao!.AlertaEnviado = true;
                    semIdentificacao.AlertaEnviadoEmUtc = enviadoEm;
                }
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }

        await FinalizarCameraAsync(camera, analise.Uso, agora, enviadoEm);

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
        var abertaDesde = agora - DuracaoMaximaDaOcorrencia;
        var vistaDesde = agora - PausaQueEncerraSemIdentificacao;
        var candidatas = _db.OcorrenciasEpiCamera.Where(o => o.CameraId == camera.Id && o.RecebidaEmUtc >= abertaDesde);
        candidatas = trabalhadorId is { } id
            ? candidatas.Where(o => o.TrabalhadorId == id)
            : candidatas.Where(o => o.TrabalhadorId == null && (o.UltimaVezEmUtc ?? o.RecebidaEmUtc) >= vistaDesde);

        var existente = await candidatas.OrderByDescending(o => o.RecebidaEmUtc).FirstOrDefaultAsync(CancellationToken.None);
        if (existente is not null)
        {
            existente.VezesVista++;
            existente.UltimaVezEmUtc = agora;
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
            UltimaVezEmUtc = agora,
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
    private async Task<List<Guid>> NotificadosNaUltimaHoraAsync(IReadOnlyCollection<Guid> trabalhadorIds, DateTime agora)
    {
        if (trabalhadorIds.Count == 0) return new List<Guid>();
        var limite = agora - IntervaloAlertasPorTrabalhador;
        return await _db.OcorrenciasEpiCamera
            .Where(o => o.TrabalhadorId != null && trabalhadorIds.Contains(o.TrabalhadorId.Value) && o.AlertaEnviado && o.AlertaEnviadoEmUtc >= limite)
            .Select(o => o.TrabalhadorId!.Value).Distinct().ToListAsync(CancellationToken.None);
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
