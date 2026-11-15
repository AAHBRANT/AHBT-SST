using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
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
// SemCapacete/SemColete: só itens avaliados e não conformes. CapaceteNaoAvaliavel/ColeteNaoAvaliavel (campos novos,
// opcionais): pessoas cujo item não foi julgado porque a parte do corpo não aparece; nunca geram ocorrência sozinhos.
// SemOculos/OculosNaoAvaliavel: o mesmo para óculos de proteção (SemOculos é irregularidade igual a capacete/colete).
// AvisoCorCapacete: a ocorrência é só de cor do capacete diferente da função (gravidade menor, sem Telegram).
public record ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete,
    Guid? TrabalhadorId = null, string? TrabalhadorNome = null, bool Repetida = false,
    int CapaceteNaoAvaliavel = 0, int ColeteNaoAvaliavel = 0,
    int SemOculos = 0, int OculosNaoAvaliavel = 0, bool AvisoCorCapacete = false);

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

    // Cena sem irregularidade só gasta identificação facial (custo e dado pessoal) para conferir cor do capacete
    // no máximo uma vez a cada 10 minutos por câmera. Cena com irregularidade identifica sempre, como antes.
    public static readonly TimeSpan IntervaloIdentificacaoSoCor = TimeSpan.FromMinutes(10);

    // Confirmação (Fase 2): a primeira vista de uma irregularidade fica "não confirmada" e sem Telegram. Ela confirma quando a MESMA
    // irregularidade (mesmo trabalhador, ou mesma pessoa vista na mesma região, sem identificação) é vista de novo depois do tempo
    // mínimo (Camera.Persistencia*Segundos, contado desde a PRIMEIRA vista). A janela é "vista há menos de 60 s" e se estende a cada
    // nova vista (pessoa parada continua confirmável). Passou 60 s sem nenhuma vista e sem confirmar: a ocorrência fica registrada
    // como não confirmada (conferência no modo sombra) e a próxima vista abre outra.
    public static readonly TimeSpan JanelaMaximaConfirmacao = TimeSpan.FromSeconds(60);

    // "Mesma posição": distância (euclidiana, em fração da imagem, 0 a 1) entre as cabeças de duas vistas, até este valor.
    public const double RaioMesmaPosicao = 0.15;

    // Ocorrência ainda NÃO confirmada e sem nome: quem anda muda de posição entre os quadros, então o raio aceito cresce com o
    // tempo desde a última vista (RaioMesmaPosicao + CrescimentoRaioPorSegundo * segundos), até RaioMaximoMesmaPosicao. Além disso,
    // se ela for a única não confirmada aberta da câmera e a cena tiver uma única pessoa irregular, qualquer posição vale.
    public const double CrescimentoRaioPorSegundo = 0.10;
    public const double RaioMaximoMesmaPosicao = 0.6;

    // Fusão identificada x não identificada: rosto reconhecido num quadro e não no seguinte (ou o contrário) na mesma posição,
    // com a outra ocorrência vista há no máximo isto, é a mesma pessoa e a mesma ocorrência.
    public static readonly TimeSpan JanelaFusaoIdentificacao = TimeSpan.FromSeconds(60);

    // Tentativas de gravar a vista (e de reservar o envio) quando outra réplica altera a mesma ocorrência ao mesmo tempo.
    private const int TentativasPorConflito = 2;

    // Quadro do agente recebido com mais atraso que isto (captura x recebimento) é "atrasado": usa o horário da captura.
    public static readonly TimeSpan ToleranciaQuadroAtrasado = TimeSpan.FromMinutes(2);

    private readonly IAppDbContext _db;
    private readonly IAnalisadorEpiImagem _analisador;
    private readonly ITelegramResumoService _telegram;
    private readonly IAutenticacaoFacialService _facial;
    private readonly ILogger<AnalisarQuadroCameraCommandHandler> _logger;

    // Ocorrências abertas pela vista em andamento (o handler é transiente: uma instância por análise). Servem para tirá-las do
    // rastreamento se a gravação conflitar e a vista precisar ser reaplicada, sem abrir a mesma ocorrência duas vezes.
    private readonly List<OcorrenciaEpiCamera> _novasNaVista = new();

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

        var irregular = analise.SemCapacete > 0 || analise.SemColete > 0 || analise.SemOculos > 0;
        var corParaConferir = analise.Detalhe is { Count: > 0 } d0 && d0.Any(p => p.CorCapaceteVista is not null);

        // Sem irregularidade real, só a cor do capacete pode gerar um aviso, e só quando alguma função tem cor definida
        // (sem isso a identificação pelo rosto, que custa uma chamada ao Azure, seria gasta à toa em cena limpa).
        if (!irregular && !(corParaConferir && await ExisteFuncaoComCorAsync(camera.ObraId) && await ReservarIdentificacaoSoCorAsync(camera, agora)))
        {
            await FinalizarCameraAsync(camera, analise, agora, alertaEnviadoEm: null);
            // Item não avaliável (parte do corpo fora da imagem) não gera ocorrência nem alerta: cena limpa.
            return Limpo(analise);
        }

        // Várias pessoas na cena, com a posição de cada uma: identifica cada irregular pelo rosto.
        if (analise.Pessoas > 1 && analise.Detalhe is { Count: > 1 } detalhe && (detalhe.Any(p => p.Irregular) || corParaConferir))
            return await RegistrarCenaComVariasPessoasAsync(camera, request, analise, detalhe, agora);

        // Uma única pessoa na cena: o rosto da foto é o dela. (Várias pessoas sem posição conhecida ficam
        // sem identificação.) Vem antes de gravar, para a repetição do mesmo trabalhador somar na ocorrência dele.
        var identificado = analise.Pessoas == 1 ? await IdentificarPeloRostoAsync(camera, request.Imagem) : null;

        // Posição da cabeça (só com a pessoa listada no detalhe): é com ela que a mesma pessoa é reconhecida de um quadro para o outro.
        IReadOnlyList<Posicao> posicoes = analise.Pessoas == 1 && analise.Detalhe is { Count: 1 } dp
            ? new[] { new Posicao(dp[0].CabecaX, dp[0].CabecaY) }
            : Array.Empty<Posicao>();

        // Rosto não reconhecido neste quadro, mas a mesma posição já tinha um trabalhador reconhecido há instantes: é ele.
        if (identificado is null && irregular && posicoes.Count > 0)
            identificado = await ProcurarIdentificadoNaMesmaPosicaoAsync(camera, request, agora, posicoes);

        // Cor do capacete: vista pela IA (só com capacete conforme) x esperada para a função de quem foi identificado.
        var corVista = analise.Pessoas == 1 && analise.Detalhe is { Count: 1 } d1 ? d1[0].CorCapaceteVista : null;
        CorCapacete? corEsperada = null;
        if (identificado is not null
            && (await ObterCoresEsperadasAsync(new[] { identificado.Id })).TryGetValue(identificado.Id, out var corDaFuncao))
            corEsperada = corDaFuncao;

        if (!irregular)
        {
            // Só aviso de cor: exige pessoa identificada, função com cor e cor vista diferente dela.
            if (identificado is null || corVista is null || corEsperada is null || corEsperada == corVista)
            {
                await FinalizarCameraAsync(camera, analise, agora, alertaEnviadoEm: null);
                return Limpo(analise);
            }

            var (aviso, novoAviso) = await GravarVistaAsync(() => ObterOuCriarOcorrenciaAsync(camera, request, analise, 0, 0, 0,
                identificado.Id, identificado.Confianca, agora,
                new InfoCor(corVista, corEsperada, Aviso: true, analise.CapaceteNaoAvaliavel, analise.ColeteNaoAvaliavel, analise.OculosNaoAvaliavel),
                posicoes));
            // Aviso de padrão nunca vai ao Telegram: fica só na aba de alertas.
            await FinalizarCameraAsync(camera, analise, agora, alertaEnviadoEm: null);
            return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, aviso.Id, false, analise.Pessoas, 0, 0,
                identificado.Id, identificado.Nome, Repetida: !novoAviso,
                CapaceteNaoAvaliavel: analise.CapaceteNaoAvaliavel, ColeteNaoAvaliavel: analise.ColeteNaoAvaliavel,
                SemOculos: 0, OculosNaoAvaliavel: analise.OculosNaoAvaliavel, AvisoCorCapacete: true);
        }

        // A ocorrência é gravada ANTES do alerta e sem o token da requisição: a chamada à IA pode demorar
        // minutos e um cancelamento do cliente ou conflito na câmera não pode perder a evidência.
        var (ocorrencia, nova) = await GravarVistaAsync(() => ObterOuCriarOcorrenciaAsync(camera, request, analise,
            analise.SemCapacete, analise.SemColete, analise.SemOculos,
            identificado?.Id, identificado?.Confianca, agora,
            new InfoCor(corVista, corEsperada, Aviso: false, analise.CapaceteNaoAvaliavel, analise.ColeteNaoAvaliavel, analise.OculosNaoAvaliavel),
            posicoes, umaIrregularNaCena: analise.Pessoas == 1));

        // Cada ocorrência alerta no máximo uma vez. Trabalhador identificado: um alerta por pessoa a cada
        // hora, em qualquer câmera (regra do usuário em 09/11/2026), no lugar do intervalo da câmera, para o
        // alerta de um não segurar o de outro. Sem identificação: vale o intervalo da câmera.
        var atrasadoEm = AtrasoDoQuadro(request, agora);
        // Só ocorrência confirmada alerta, e nunca em modo sombra (a câmera só registra). Confirmada e ainda sem alerta enviado
        // (intervalo da câmera, Telegram fora do ar) segue tentando nas próximas vistas, como antes.
        var podeAlertar = !camera.ModoSombra && camera.AvisarTelegram && ocorrencia.Confirmada && !ocorrencia.AlertaEnviado
            && (identificado is not null
                ? !(await NotificadosNaUltimaHoraAsync(new[] { identificado.Id }, agora, atrasadoEm)).Contains(identificado.Id)
                : await IntervaloDaCameraPermiteAsync(camera, agora, atrasadoEm));

        DateTime? enviadoEm = null;
        if (podeAlertar)
        {
            // Reserva o envio ANTES de mandar (várias réplicas): só quem consegue marcar a ocorrência como enviada envia.
            var reservadas = await ReservarEnvioAsync(new[] { ocorrencia }, DateTime.UtcNow);
            if (reservadas.Count > 0)
            {
                var legenda = LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome, atrasadoEm ?? agora,
                    ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete, identificado?.Nome,
                    analise.CapaceteNaoAvaliavel, analise.ColeteNaoAvaliavel, ocorrencia.SemOculos, analise.OculosNaoAvaliavel)
                    + LegendaAlertaEpi.AvisoDeAtraso(atrasadoEm);
                if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
                    enviadoEm = ocorrencia.AlertaEnviadoEmUtc;
                else
                    await DesfazerReservaAsync(reservadas);
            }
        }

        // Alerta de quadro atrasado não ocupa o intervalo entre alertas da câmera: esse intervalo é dos eventos ao vivo.
        await FinalizarCameraAsync(camera, analise, agora, atrasadoEm is null ? enviadoEm : null);

        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, ocorrencia.Id, enviadoEm is not null,
            analise.Pessoas, analise.SemCapacete, analise.SemColete, identificado?.Id, identificado?.Nome, Repetida: !nova,
            CapaceteNaoAvaliavel: analise.CapaceteNaoAvaliavel, ColeteNaoAvaliavel: analise.ColeteNaoAvaliavel,
            SemOculos: analise.SemOculos, OculosNaoAvaliavel: analise.OculosNaoAvaliavel);
    }

    private static ResultadoQuadroDto Limpo(ResultadoAnaliseEpi analise)
        => new(SituacaoQuadro.Limpo, null, false, analise.Pessoas, 0, 0,
            CapaceteNaoAvaliavel: analise.CapaceteNaoAvaliavel, ColeteNaoAvaliavel: analise.ColeteNaoAvaliavel,
            OculosNaoAvaliavel: analise.OculosNaoAvaliavel);

    // Cor do capacete que vai junto da ocorrência. Aviso = a ocorrência existe só por causa da cor.
    // As três contagens são as pessoas da ocorrência cujo item não pôde ser julgado (só informação na tela).
    private sealed record InfoCor(CorCapacete? Vista, CorCapacete? Esperada, bool Aviso,
        int CapaceteNaoAvaliavel = 0, int ColeteNaoAvaliavel = 0, int OculosNaoAvaliavel = 0);

    // Só vale conferir cor se algum trabalhador ATIVO da obra da câmera tem função com cor definida. Consultas separadas,
    // sem JOIN (função excluída ou filtro de escopo não podem esconder o resultado na rota anônima do agente).
    private async Task<bool> ExisteFuncaoComCorAsync(Guid obraId)
    {
        var funcaoIds = await _db.Funcoes.IgnoreQueryFilters().Where(f => f.CorCapacete != CorCapacete.SemPadrao)
            .Select(f => f.Id).ToListAsync(CancellationToken.None);
        if (funcaoIds.Count == 0) return false;
        return await _db.Trabalhadores.AnyAsync(
            t => t.ObraId == obraId && t.DataDemissao == null && funcaoIds.Contains(t.FuncaoId), CancellationToken.None);
    }

    // Limite de uma identificação "só de cor" a cada IntervaloIdentificacaoSoCor por câmera, guardado no banco. A gravação
    // concorre pela versão da linha da câmera: quem perde (outra réplica reservou no mesmo instante) não identifica.
    private async Task<bool> ReservarIdentificacaoSoCorAsync(Camera camera, DateTime agora)
    {
        if (camera.UltimaIdentificacaoSoCorEmUtc is { } ultima && agora - ultima < IntervaloIdentificacaoSoCor) return false;
        camera.UltimaIdentificacaoSoCorEmUtc = agora;
        try
        {
            await _db.SaveChangesAsync(CancellationToken.None);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    // Cor esperada (função do trabalhador) por trabalhador; quem está sem função com cor definida não aparece.
    // Duas consultas simples, sem JOIN: a rota do agente é anônima e o filtro de escopo da Trabalhador não pode
    // derrubar o resultado. Função excluída (inativa) continua valendo.
    private async Task<Dictionary<Guid, CorCapacete>> ObterCoresEsperadasAsync(IReadOnlyCollection<Guid> trabalhadorIds)
    {
        if (trabalhadorIds.Count == 0) return new Dictionary<Guid, CorCapacete>();
        var funcaoPorTrabalhador = await _db.Trabalhadores.Where(t => trabalhadorIds.Contains(t.Id))
            .Select(t => new { t.Id, t.FuncaoId }).ToListAsync(CancellationToken.None);
        var funcaoIds = funcaoPorTrabalhador.Select(t => t.FuncaoId).Distinct().ToList();
        var cores = await _db.Funcoes.IgnoreQueryFilters().Where(f => funcaoIds.Contains(f.Id) && f.CorCapacete != CorCapacete.SemPadrao)
            .Select(f => new { f.Id, f.CorCapacete }).ToDictionaryAsync(f => f.Id, f => f.CorCapacete, CancellationToken.None);

        var resultado = new Dictionary<Guid, CorCapacete>();
        foreach (var t in funcaoPorTrabalhador)
            if (cores.TryGetValue(t.FuncaoId, out var cor)) resultado[t.Id] = cor;
        return resultado;
    }

    // Cena com várias pessoas. Cada irregular identificado pelo rosto tem a própria ocorrência (com o que
    // faltava nele), para a conferência e a regra de um alerta por trabalhador por hora valerem por pessoa.
    // Os irregulares sem identificação ficam juntos numa ocorrência sem trabalhador. Sai um único alerta
    // por foto, com os nomes de quem ainda não foi notificado na última hora.
    // Cor do capacete: quem está em ordem com o EPI, foi identificado e tem capacete de cor diferente da esperada
    // para a função ganha uma ocorrência só de aviso (sem Telegram). Quem é irregular leva a cor junto, como informação.
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
        var coresEsperadas = await ObterCoresEsperadasAsync(ids);

        var vistas = await GravarVistaAsync(() => RegistrarVistasDaCenaAsync(camera, request, analise, detalhe, agora,
            rostoPorPessoa, nomes, ids, coresEsperadas));
        var identificadas = vistas.Identificadas;
        var avisosDeCor = vistas.AvisosDeCor;
        var naoIdentificados = vistas.NaoIdentificados;
        var semIdentificacao = vistas.SemIdentificacao;
        var algumaNova = vistas.AlgumaNova;

        var totalSemCapacete = detalhe.Count(p => p.SemCapacete);
        var totalSemColete = detalhe.Count(p => p.SemColete);
        var totalSemOculos = detalhe.Count(p => p.SemOculos);

        // Nenhum irregular: só avisos de cor (ou nada). Aviso de padrão nunca vai ao Telegram.
        if (identificadas.Count == 0 && semIdentificacao is null)
        {
            await FinalizarCameraAsync(camera, analise, agora, alertaEnviadoEm: null);
            if (avisosDeCor.Count == 0) return Limpo(analise);
            return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, avisosDeCor[0].Id, false, analise.Pessoas, 0, 0,
                avisosDeCor.Count == 1 ? avisosDeCor[0].TrabalhadorId : null,
                string.Join(", ", avisosDeCor.Select(a => nomes[a.TrabalhadorId!.Value])),
                Repetida: !algumaNova,
                CapaceteNaoAvaliavel: analise.CapaceteNaoAvaliavel, ColeteNaoAvaliavel: analise.ColeteNaoAvaliavel,
                OculosNaoAvaliavel: analise.OculosNaoAvaliavel, AvisoCorCapacete: true);
        }

        // Quem já foi notificado na última hora (em qualquer câmera) fica fora do alerta desta foto.
        var atrasadoEm = AtrasoDoQuadro(request, agora);
        var notificadosNaHora = await NotificadosNaUltimaHoraAsync(ids, agora, atrasadoEm);
        // Só ocorrência confirmada alerta, e nunca em modo sombra (a câmera só registra).
        var podeEnviar = !camera.ModoSombra && camera.AvisarTelegram;
        var aAlertar = !podeEnviar
            ? new List<(OcorrenciaEpiCamera Ocorrencia, IdentificadoNaCena Pessoa)>()
            : identificadas
                .Where(i => i.Ocorrencia.Confirmada && !i.Ocorrencia.AlertaEnviado && !notificadosNaHora.Contains(i.Ocorrencia.TrabalhadorId!.Value))
                .ToList();

        // Sem identificação vale o intervalo da câmera, como na cena de uma pessoa só.
        var alertaSemIdentificacao = podeEnviar && semIdentificacao is { AlertaEnviado: false, Confirmada: true }
            && await IntervaloDaCameraPermiteAsync(camera, agora, atrasadoEm);

        DateTime? enviadoEm = null;
        if (aAlertar.Count > 0 || alertaSemIdentificacao)
        {
            // Reserva o envio ANTES de mandar (várias réplicas): só entra na legenda quem esta réplica conseguiu marcar.
            var candidatas = aAlertar.Select(i => i.Ocorrencia).ToList();
            if (alertaSemIdentificacao) candidatas.Add(semIdentificacao!);
            var reservadas = await ReservarEnvioAsync(candidatas, DateTime.UtcNow);
            if (reservadas.Count > 0)
            {
                var aAlertarReservadas = aAlertar.Where(i => reservadas.Contains(i.Ocorrencia)).Select(i => i.Pessoa).ToList();
                var semIdentificacaoReservada = alertaSemIdentificacao && reservadas.Contains(semIdentificacao!);
                var legenda = LegendaAlertaEpi.MontarCena(camera.Obra?.Nome ?? "-", camera.Nome, atrasadoEm ?? agora, analise.Pessoas,
                    totalSemCapacete, totalSemColete, aAlertarReservadas,
                    semIdentificacaoReservada ? naoIdentificados.Count : 0, analise.CapaceteNaoAvaliavel, analise.ColeteNaoAvaliavel,
                    totalSemOculos, analise.OculosNaoAvaliavel)
                    + LegendaAlertaEpi.AvisoDeAtraso(atrasadoEm);
                if (await EnviarLegendaAsync(camera, request.Imagem, legenda))
                    enviadoEm = reservadas[0].AlertaEnviadoEmUtc;
                else
                    await DesfazerReservaAsync(reservadas);
            }
        }

        await FinalizarCameraAsync(camera, analise, agora, atrasadoEm is null ? enviadoEm : null);

        var primeira = identificadas.Select(i => i.Ocorrencia).FirstOrDefault() ?? semIdentificacao!;
        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, primeira.Id, enviadoEm is not null,
            analise.Pessoas, totalSemCapacete, totalSemColete,
            identificadas.Count == 1 ? identificadas[0].Ocorrencia.TrabalhadorId : null,
            identificadas.Count == 0 ? null : string.Join(", ", identificadas.Select(i => i.Pessoa.Nome)),
            Repetida: !algumaNova,
            CapaceteNaoAvaliavel: analise.CapaceteNaoAvaliavel, ColeteNaoAvaliavel: analise.ColeteNaoAvaliavel,
            SemOculos: totalSemOculos, OculosNaoAvaliavel: analise.OculosNaoAvaliavel);
    }

    private sealed record VistasDaCena(
        List<(OcorrenciaEpiCamera Ocorrencia, IdentificadoNaCena Pessoa)> Identificadas,
        List<OcorrenciaEpiCamera> AvisosDeCor,
        List<PessoaNaCena> NaoIdentificados,
        OcorrenciaEpiCamera? SemIdentificacao,
        bool AlgumaNova);

    // Registra a vista de cada pessoa da cena (não grava: quem chama salva, com nova tentativa se houver conflito).
    // Idempotente em relação a uma segunda execução depois de GravarVistaAsync descartar o que ficou pendente.
    private async Task<VistasDaCena> RegistrarVistasDaCenaAsync(
        Camera camera, AnalisarQuadroCameraCommand request, ResultadoAnaliseEpi analise, IReadOnlyList<PessoaNaCena> detalhe, DateTime agora,
        IReadOnlyDictionary<int, RostoNaCena> rostoPorPessoa, Dictionary<Guid, string> nomes, List<Guid> ids,
        IReadOnlyDictionary<Guid, CorCapacete> coresEsperadas)
    {
        var identificadas = new List<(OcorrenciaEpiCamera Ocorrencia, IdentificadoNaCena Pessoa)>();
        var avisosDeCor = new List<OcorrenciaEpiCamera>();
        var naoIdentificados = new List<PessoaNaCena>();
        var jaIncluidos = new HashSet<Guid>();
        var algumaNova = false;
        for (var i = 0; i < detalhe.Count; i++)
        {
            var pessoa = detalhe[i];

            // O mesmo trabalhador não entra duas vezes na mesma foto, nem um que o banco não enxerga.
            var identificadoNaFoto = rostoPorPessoa.TryGetValue(i, out var rosto) && rosto.TrabalhadorId is { } trabalhadorId
                && nomes.ContainsKey(trabalhadorId);

            if (!pessoa.Irregular)
            {
                // Em ordem com o EPI: só interessa o aviso de cor, e só com pessoa identificada, função com cor e cor diferente.
                if (identificadoNaFoto && pessoa.CorCapaceteVista is { } vista
                    && coresEsperadas.TryGetValue(rosto!.TrabalhadorId!.Value, out var esperadaDoAviso) && esperadaDoAviso != vista
                    && jaIncluidos.Add(rosto.TrabalhadorId!.Value))
                {
                    var (aviso, novoAviso) = await ObterOuCriarOcorrenciaAsync(camera, request, analise, 0, 0, 0,
                        rosto.TrabalhadorId!.Value, rosto.Confianca, agora,
                        new InfoCor(vista, esperadaDoAviso, Aviso: true, pessoa.CapaceteNaoAvaliavel ? 1 : 0, pessoa.ColeteNaoAvaliavel ? 1 : 0, pessoa.OculosNaoAvaliavel ? 1 : 0),
                        new[] { new Posicao(pessoa.CabecaX, pessoa.CabecaY) });
                    algumaNova |= novoAviso;
                    avisosDeCor.Add(aviso);
                }
                continue;
            }

            if (identificadoNaFoto && jaIncluidos.Add(rosto!.TrabalhadorId!.Value))
            {
                var id = rosto.TrabalhadorId!.Value;
                CorCapacete? esperada = coresEsperadas.TryGetValue(id, out var corDaFuncao) ? corDaFuncao : null;
                var (ocorrencia, nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise,
                    pessoa.SemCapacete ? 1 : 0, pessoa.SemColete ? 1 : 0, pessoa.SemOculos ? 1 : 0, id, rosto.Confianca, agora,
                    new InfoCor(pessoa.CorCapaceteVista, esperada, Aviso: false,
                        pessoa.CapaceteNaoAvaliavel ? 1 : 0, pessoa.ColeteNaoAvaliavel ? 1 : 0, pessoa.OculosNaoAvaliavel ? 1 : 0),
                    new[] { new Posicao(pessoa.CabecaX, pessoa.CabecaY) });
                algumaNova |= nova;
                identificadas.Add((ocorrencia, new IdentificadoNaCena(nomes[id], pessoa.SemCapacete, pessoa.SemColete, pessoa.SemOculos)));
            }
            else if (!identificadoNaFoto
                && await ProcurarIdentificadoNaMesmaPosicaoAsync(camera, request, agora, new[] { new Posicao(pessoa.CabecaX, pessoa.CabecaY) }) is { } mesmaPosicao
                && jaIncluidos.Add(mesmaPosicao.Id))
            {
                // Rosto não reconhecido neste quadro, mas a mesma posição já tinha um trabalhador reconhecido há instantes: é ele.
                var (ocorrencia, nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise,
                    pessoa.SemCapacete ? 1 : 0, pessoa.SemColete ? 1 : 0, pessoa.SemOculos ? 1 : 0, mesmaPosicao.Id, null, agora,
                    new InfoCor(pessoa.CorCapaceteVista, null, Aviso: false,
                        pessoa.CapaceteNaoAvaliavel ? 1 : 0, pessoa.ColeteNaoAvaliavel ? 1 : 0, pessoa.OculosNaoAvaliavel ? 1 : 0),
                    new[] { new Posicao(pessoa.CabecaX, pessoa.CabecaY) });
                algumaNova |= nova;
                nomes[mesmaPosicao.Id] = mesmaPosicao.Nome;
                ids.Add(mesmaPosicao.Id);
                identificadas.Add((ocorrencia, new IdentificadoNaCena(mesmaPosicao.Nome, pessoa.SemCapacete, pessoa.SemColete, pessoa.SemOculos)));
            }
            else
            {
                naoIdentificados.Add(pessoa);
            }
        }

        OcorrenciaEpiCamera? semIdentificacao = null;
        var umaIrregularNaCena = detalhe.Count(p => p.Irregular) == 1;
        if (naoIdentificados.Count > 0)
        {
            // Sem identificação não se acusa cor: a cor vista só é gravada quando todos os irregulares sem nome a viram igual.
            var coresVistas = naoIdentificados.Where(p => p.CorCapaceteVista is not null).Select(p => p.CorCapaceteVista).Distinct().ToList();
            (semIdentificacao, var nova) = await ObterOuCriarOcorrenciaAsync(camera, request, analise,
                naoIdentificados.Count(p => p.SemCapacete), naoIdentificados.Count(p => p.SemColete), naoIdentificados.Count(p => p.SemOculos),
                null, null, agora, new InfoCor(coresVistas.Count == 1 ? coresVistas[0] : null, null, Aviso: false,
                    naoIdentificados.Count(p => p.CapaceteNaoAvaliavel), naoIdentificados.Count(p => p.ColeteNaoAvaliavel),
                    naoIdentificados.Count(p => p.OculosNaoAvaliavel)),
                naoIdentificados.Select(p => new Posicao(p.CabecaX, p.CabecaY)).ToList(), umaIrregularNaCena);
            algumaNova |= nova;
        }
        return new VistasDaCena(identificadas, avisosDeCor, naoIdentificados, semIdentificacao, algumaNova);
    }

    // Posição aproximada da cabeça (fração da imagem, 0 a 1).
    private readonly record struct Posicao(double X, double Y);

    private static double Distancia(double x, double y, Posicao p) => Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));

    // Menor distância entre a posição guardada na ocorrência e as posições vistas agora; nulo = não dá para comparar (alguma das
    // duas pontas sem posição, como nas ocorrências anteriores à Fase 2).
    private static double? DistanciaAte(OcorrenciaEpiCamera o, IReadOnlyList<Posicao> posicoes)
        => o.PosicaoX is { } x && o.PosicaoY is { } y && posicoes.Count > 0 ? posicoes.Min(p => Distancia(x, y, p)) : null;

    // Instante da primeira vista da ocorrência, na mesma base do quadro que está sendo analisado: horário real da captura quando o
    // quadro vem marcado como atrasado (agente sem internet) e horário de recebimento nos demais (o relógio do tablet/câmera não é
    // confiável para quadro ao vivo).
    private static DateTime InicioDaVista(OcorrenciaEpiCamera o, bool quadroAtrasado)
        => quadroAtrasado ? o.CapturadaEmUtc : o.RecebidaEmUtc;

    private static int SegundosEntre(DateTime de, DateTime ate) => (int)Math.Max(0, Math.Round((ate - de).TotalSeconds));

    // A irregularidade PERSISTIU quando QUALQUER item irregular na vista anterior tem QUALQUER item irregular agora (a mesma pessoa
    // segue irregular, mesmo que mude o item: 1ª sem capacete, 2ª sem colete) e já passou o tempo mínimo desde a primeira vista.
    // O tempo mínimo é o menor entre os itens irregulares agora (Camera.Persistencia*Segundos); aviso de cor usa o do capacete.
    // Tempo mínimo 0 (só alcançável gravando direto no banco; a validação exige 1 a 30) confirma já na primeira vista.
    private static bool Persistiu(Camera camera, bool aviso, double segundos,
        (int Capacete, int Colete, int Oculos) antes, (int Capacete, int Colete, int Oculos) agora)
    {
        if (aviso) return segundos >= camera.PersistenciaCapaceteSegundos;
        if (antes.Capacete + antes.Colete + antes.Oculos == 0) return false;
        var minimos = new List<int>(3);
        if (agora.Capacete > 0) minimos.Add(camera.PersistenciaCapaceteSegundos);
        if (agora.Colete > 0) minimos.Add(camera.PersistenciaColeteSegundos);
        if (agora.Oculos > 0) minimos.Add(camera.PersistenciaOculosSegundos);
        return minimos.Count > 0 && segundos >= minimos.Min();
    }

    // Grava o que a vista alterou. Se outra réplica alterou a mesma ocorrência neste instante (conflito de versão), descarta o que
    // ficou pendente (novas saem do rastreamento, existentes são recarregadas do banco) e reaplica a vista UMA vez.
    private async Task<T> GravarVistaAsync<T>(Func<Task<T>> aplicar)
    {
        _novasNaVista.Clear();
        var resultado = await aplicar();
        DbUpdateConcurrencyException? conflito = null;
        try
        {
            await _db.SaveChangesAsync(CancellationToken.None);
            return resultado;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            conflito = ex;
            _logger.LogInformation(ex, "Ocorrência alterada em paralelo; vista reaplicada.");
        }

        // Remove numa entidade ainda não gravada só a tira do rastreamento. As existentes que conflitaram voltam ao valor do banco.
        foreach (var nova in _novasNaVista) _db.OcorrenciasEpiCamera.Remove(nova);
        _novasNaVista.Clear();
        foreach (var entrada in conflito!.Entries) await entrada.ReloadAsync(CancellationToken.None);
        resultado = await aplicar();
        await _db.SaveChangesAsync(CancellationToken.None);
        return resultado;
    }

    // Reserva o envio do alerta marcando as ocorrências como enviadas ANTES de chamar o Telegram. A gravação concorre pela versão
    // da linha: se outra réplica já marcou (ou alterou) a ocorrência, o conflito recarrega o valor do banco e quem já a encontra
    // marcada não envia. Devolve só as ocorrências que ESTA réplica reservou.
    private async Task<List<OcorrenciaEpiCamera>> ReservarEnvioAsync(IReadOnlyCollection<OcorrenciaEpiCamera> candidatas, DateTime quando)
    {
        var pendentes = candidatas.ToList();
        for (var tentativa = 1; tentativa <= TentativasPorConflito && pendentes.Count > 0; tentativa++)
        {
            foreach (var o in pendentes)
            {
                o.AlertaEnviado = true;
                o.AlertaEnviadoEmUtc = quando;
            }
            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
                return pendentes;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Só as ocorrências que conflitaram são recarregadas do banco; se já estão marcadas, outra réplica reservou.
                var conflitantes = new HashSet<object>(ex.Entries.Select(e => e.Entity), ReferenceEqualityComparer.Instance);
                foreach (var entrada in ex.Entries) await entrada.ReloadAsync(CancellationToken.None);
                pendentes = pendentes.Where(o => !conflitantes.Contains(o) || !o.AlertaEnviado).ToList();
            }
        }
        return new List<OcorrenciaEpiCamera>();
    }

    // O Telegram recusou ou falhou: devolve a ocorrência a "alerta não enviado" para uma próxima vista tentar de novo.
    private async Task DesfazerReservaAsync(IReadOnlyCollection<OcorrenciaEpiCamera> reservadas)
    {
        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            foreach (var o in reservadas)
            {
                o.AlertaEnviado = false;
                o.AlertaEnviadoEmUtc = null;
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
                    _logger.LogWarning(ex, "Reserva de alerta não desfeita após {Tentativas} tentativas.", tentativa);
                    return;
                }
                foreach (var entrada in ex.Entries) await entrada.ReloadAsync(CancellationToken.None);
            }
        }
    }

    // Trabalhador já reconhecido, nesta câmera, na mesma posição da cabeça e há pouco (JanelaFusaoIdentificacao): corrige o rosto
    // que foi reconhecido num quadro e não no seguinte. Só olha ocorrência irregular (não aviso de cor) do mesmo modo (sombra ou não).
    private async Task<Identificado?> ProcurarIdentificadoNaMesmaPosicaoAsync(
        Camera camera, AnalisarQuadroCameraCommand request, DateTime agora, IReadOnlyList<Posicao> posicoes)
    {
        var referencia = AtrasoDoQuadro(request, agora) ?? agora;
        var desde = referencia - JanelaFusaoIdentificacao;
        var candidatas = await _db.OcorrenciasEpiCamera
            .Where(o => o.CameraId == camera.Id && o.TrabalhadorId != null && !o.AvisoCorCapacete && o.Sombra == camera.ModoSombra
                && o.PosicaoX != null && (o.UltimaVezEmUtc ?? o.RecebidaEmUtc) >= desde)
            .ToListAsync(CancellationToken.None);
        var mesma = candidatas
            .Select(o => (Ocorrencia: o, Distancia: DistanciaAte(o, posicoes)))
            .Where(c => c.Distancia is { } d && d <= RaioMesmaPosicao)
            .OrderBy(c => c.Distancia)
            .Select(c => c.Ocorrencia)
            .FirstOrDefault();
        if (mesma?.TrabalhadorId is not { } id) return null;

        var nome = await _db.Trabalhadores.Where(t => t.Id == id).Select(t => t.Nome).FirstOrDefaultAsync(CancellationToken.None);
        return nome is null ? null : new Identificado(id, nome, null);
    }

    // Soma a irregularidade numa ocorrência já aberta (mesma câmera e mesmo trabalhador, ou a mesma cena sem
    // identificação) ou abre uma nova. Na repetição a foto e o horário continuam os da primeira vez; contam
    // as vezes vistas, a última vez e o pior caso das contagens. Não grava: quem chama salva.
    // cor: cor do capacete que vai junto. Aviso = só cor (as contagens vêm zeradas). Aviso e ocorrência irregular NUNCA se
    // juntam: aviso só soma em aviso e irregular só em irregular. Aviso aberto + irregularidade nova = ocorrência nova, com a
    // foto e o horário do quadro irregular; irregular aberta + aviso novo não mexe nas vezes vistas da irregular.
    //
    // Fase 2 (tudo guardado na própria ocorrência, sem estado em memória: a API tem várias réplicas):
    //  - Registro em modo sombra e registro com aviso nunca se juntam (Sombra da ocorrência = ModoSombra da câmera no momento);
    //    ligar ou desligar o modo sombra no meio de uma cena abre outra ocorrência e o aviso volta (ou para) já na vista seguinte.
    //  - Ocorrência irregular nova nasce NÃO confirmada. Vista de novo (mesma pessoa) depois do tempo mínimo, contado desde a
    //    primeira vista: Confirmada. Aviso de cor nasce confirmado (nunca vai ao Telegram).
    //  - Não confirmada só recebe vista se foi vista há menos de JanelaMaximaConfirmacao (a janela se estende a cada vista).
    //    Sem nome, a mesma pessoa pode ter andado: vale a posição dentro de um raio que cresce com o tempo, ou ser a única não
    //    confirmada da câmera numa cena com uma só pessoa irregular.
    //  - Não repetir (sem nome): confirmada, vista há menos de Camera.NaoRepetirSegundos (0 = desligado) e na mesma posição,
    //    absorve a vista em vez de abrir outra. Com nome vale o prazo de uma hora por trabalhador.
    //  - Fusão: trabalhador reconhecido numa posição onde havia ocorrência sem nome (vista há até JanelaFusaoIdentificacao, de
    //    uma pessoa só) assume essa ocorrência, que passa a ser dele.
    private async Task<(OcorrenciaEpiCamera Ocorrencia, bool Nova)> ObterOuCriarOcorrenciaAsync(
        Camera camera, AnalisarQuadroCameraCommand request, ResultadoAnaliseEpi analise, int semCapacete, int semColete, int semOculos,
        Guid? trabalhadorId, double? confiancaIdentificacao, DateTime agora, InfoCor? cor = null, IReadOnlyList<Posicao>? posicoes = null,
        bool umaIrregularNaCena = false)
    {
        posicoes ??= Array.Empty<Posicao>();

        // Agente: as janelas contam do instante REAL do quadro (captura, se atrasado) e olham o horário de captura das
        // ocorrências, para um quadro atrasado só agrupar com eventos da mesma época e um evento ao vivo não cair numa
        // ocorrência antiga gravada agora. No navegador (flag desligada) segue tudo como antes.
        var atrasadoEm = AtrasoDoQuadro(request, agora);
        var quadroAtrasado = atrasadoEm is not null;
        var referencia = atrasadoEm ?? agora;
        var abertaDesde = referencia - DuracaoMaximaDaOcorrencia;
        var naoRepetir = TimeSpan.FromSeconds(camera.NaoRepetirSegundos);
        var vistaDesdeNaoRepetir = referencia - naoRepetir;
        var confirmacaoDesde = referencia - JanelaMaximaConfirmacao;
        var fusaoDesde = referencia - JanelaFusaoIdentificacao;
        var ehAviso = cor?.Aviso == true;
        var sombra = camera.ModoSombra;

        var candidatas = _db.OcorrenciasEpiCamera.Where(o => o.CameraId == camera.Id && o.RecebidaEmUtc >= abertaDesde
            && o.AvisoCorCapacete == ehAviso && o.Sombra == sombra);
        if (request.PermitirQuadroAtrasado)
        {
            var limiteSuperior = referencia + (trabalhadorId is null
                ? (naoRepetir > JanelaMaximaConfirmacao ? naoRepetir : JanelaMaximaConfirmacao)
                : DuracaoMaximaDaOcorrencia);
            candidatas = candidatas.Where(o => o.CapturadaEmUtc >= abertaDesde && o.CapturadaEmUtc <= limiteSuperior);
        }
        // Só recebe vista a confirmada ou a não confirmada vista há menos de JanelaMaximaConfirmacao.
        candidatas = candidatas.Where(o => o.Confirmada || (o.UltimaVezEmUtc ?? o.RecebidaEmUtc) >= confirmacaoDesde);

        OcorrenciaEpiCamera? existente;
        var fundiu = false;
        if (trabalhadorId is { } id)
        {
            existente = await candidatas.Where(o => o.TrabalhadorId == id)
                .OrderByDescending(o => o.RecebidaEmUtc).FirstOrDefaultAsync(CancellationToken.None);

            if (existente is null && posicoes.Count > 0)
            {
                var semNome = await candidatas
                    .Where(o => o.TrabalhadorId == null && o.PessoasNaCena <= 1 && o.PosicaoX != null && (o.UltimaVezEmUtc ?? o.RecebidaEmUtc) >= fusaoDesde)
                    .ToListAsync(CancellationToken.None);
                existente = semNome
                    .Select(o => (Ocorrencia: o, Distancia: DistanciaAte(o, posicoes)))
                    .Where(c => c.Distancia is { } d && d <= RaioMesmaPosicao)
                    .OrderBy(c => c.Distancia)
                    .Select(c => c.Ocorrencia)
                    .FirstOrDefault();
                fundiu = existente is not null;
            }
        }
        else
        {
            var semNome = await candidatas.Where(o => o.TrabalhadorId == null)
                .OrderByDescending(o => o.RecebidaEmUtc).ToListAsync(CancellationToken.None);
            var naoConfirmadasAbertas = await candidatas.CountAsync(o => !o.Confirmada, CancellationToken.None);

            // Sem posição de um dos lados não dá para comparar: a ocorrência vale (o prazo é o de "não repetir" para a confirmada;
            // a não confirmada já foi limitada pela janela de confirmação).
            bool Casa(OcorrenciaEpiCamera o)
            {
                var ultima = o.UltimaVezEmUtc ?? o.RecebidaEmUtc;
                var distancia = DistanciaAte(o, posicoes);
                if (o.Confirmada)
                    return ultima >= vistaDesdeNaoRepetir && (distancia is null || distancia <= RaioMesmaPosicao);

                if (distancia is null) return true;
                var segundos = Math.Max(0, (referencia - ultima).TotalSeconds);
                var raio = Math.Min(RaioMaximoMesmaPosicao, RaioMesmaPosicao + CrescimentoRaioPorSegundo * segundos);
                // A única não confirmada da câmera, numa cena com uma só pessoa irregular, é essa pessoa onde quer que esteja.
                return distancia <= raio || (umaIrregularNaCena && naoConfirmadasAbertas == 1);
            }

            existente = semNome.FirstOrDefault(Casa);
        }

        var novasPosicao = posicoes.Count == 0 ? (Posicao?)null : posicoes[0];
        if (existente is not null)
        {
            var antes = (existente.SemCapacete, existente.SemColete, existente.SemOculos);
            existente.VezesVista++;
            if (existente.UltimaVezEmUtc is not { } ultimaVez || referencia > ultimaVez) existente.UltimaVezEmUtc = referencia;
            existente.PessoasNaCena = Math.Max(existente.PessoasNaCena, analise.Pessoas);
            existente.SemCapacete = Math.Max(existente.SemCapacete, semCapacete);
            existente.SemColete = Math.Max(existente.SemColete, semColete);
            existente.SemOculos = Math.Max(existente.SemOculos, semOculos);
            if (cor is not null)
            {
                existente.CapaceteNaoAvaliavel = Math.Max(existente.CapaceteNaoAvaliavel, cor.CapaceteNaoAvaliavel);
                existente.ColeteNaoAvaliavel = Math.Max(existente.ColeteNaoAvaliavel, cor.ColeteNaoAvaliavel);
                existente.OculosNaoAvaliavel = Math.Max(existente.OculosNaoAvaliavel, cor.OculosNaoAvaliavel);
                existente.CorCapaceteVista ??= cor.Vista;
                existente.CorCapaceteEsperada ??= cor.Esperada;
            }
            if (fundiu)
            {
                existente.TrabalhadorId = trabalhadorId;
                existente.IdentificadoPorRosto = true;
                existente.ConfiancaIdentificacao = confiancaIdentificacao;
                existente.VinculadoEmUtc = agora;
            }
            else if (existente.IdentificadoPorRosto && confiancaIdentificacao is { } confianca
                && confianca > (existente.ConfiancaIdentificacao ?? 0))
                existente.ConfiancaIdentificacao = confianca;

            // Acompanha a pessoa: a próxima vista é comparada com a posição mais recente, não com a primeira.
            if (posicoes.Count > 0 && existente.PosicaoX is { } px && existente.PosicaoY is { } py)
            {
                var maisPerto = posicoes.OrderBy(p => Distancia(px, py, p)).First();
                existente.PosicaoX = maisPerto.X;
                existente.PosicaoY = maisPerto.Y;
            }
            else if (novasPosicao is { } np)
            {
                existente.PosicaoX = np.X;
                existente.PosicaoY = np.Y;
            }

            // Segundos observados contam até confirmar (ou até a última vista, enquanto não confirma); depois de confirmada ficam como estão.
            var inicio = InicioDaVista(existente, quadroAtrasado);
            if (!existente.Confirmada)
            {
                existente.SegundosObservados = SegundosEntre(inicio, referencia);
                if (Persistiu(camera, ehAviso, (referencia - inicio).TotalSeconds, antes, (semCapacete, semColete, semOculos)))
                {
                    existente.Confirmada = true;
                    existente.ConfirmadaEmUtc = referencia;
                }
            }
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
            SemOculos = semOculos,
            Confianca = analise.Confianca,
            CapaceteNaoAvaliavel = cor?.CapaceteNaoAvaliavel ?? 0,
            ColeteNaoAvaliavel = cor?.ColeteNaoAvaliavel ?? 0,
            OculosNaoAvaliavel = cor?.OculosNaoAvaliavel ?? 0,
            CorCapaceteVista = cor?.Vista,
            CorCapaceteEsperada = cor?.Esperada,
            AvisoCorCapacete = ehAviso,
            Sombra = sombra,
            SegundosObservados = 0,
            PosicaoX = novasPosicao?.X,
            PosicaoY = novasPosicao?.Y,
        };
        // Aviso de cor nasce confirmado (nunca vai ao Telegram, não há o que confirmar). Irregularidade nasce não confirmada, e só
        // confirma de cara com tempo mínimo 0 (fora do que a tela permite).
        var itens = (semCapacete, semColete, semOculos);
        nova.Confirmada = ehAviso || Persistiu(camera, false, 0, itens, itens);
        if (nova.Confirmada) nova.ConfirmadaEmUtc = referencia;
        if (trabalhadorId is not null)
        {
            nova.TrabalhadorId = trabalhadorId;
            nova.IdentificadoPorRosto = true;
            nova.ConfiancaIdentificacao = confiancaIdentificacao;
            nova.VinculadoEmUtc = agora;
        }
        _db.OcorrenciasEpiCamera.Add(nova);
        _novasNaVista.Add(nova);
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

    private static bool IntervaloDaCameraPermite(Camera camera, DateTime agora)
        => camera.IntervaloMinimoAlertasMinutos == 0
           || camera.UltimoAlertaEmUtc is null
           || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);

    // Última gravação da análise, sempre na câmera: consumo real de tokens e, se saiu alerta, o horário dele.
    // Fica por último e isolada porque a câmera pode ter sido editada durante a análise; nesse conflito só se
    // perde o consumo e o intervalo entre alertas desta vez, nunca a ocorrência.
    private async Task FinalizarCameraAsync(Camera camera, ResultadoAnaliseEpi analise, DateTime agora, DateTime? alertaEnviadoEm)
    {
        var uso = analise.Uso;
        var hojeEmBrasilia = agora.AddHours(-3).Date;
        if (camera.DiaDoConsumo != hojeEmBrasilia)
        {
            camera.DiaDoConsumo = hojeEmBrasilia;
            camera.AnalisesNoDia = 0;
            camera.TokensEntradaNoDia = 0;
            camera.TokensSaidaNoDia = 0;
            camera.NaoAvaliaveisNoDia = 0;
        }
        camera.AnalisesNoDia++;
        // Só itens não conferidos e nenhuma irregularidade: a câmera não está enxergando o que deveria (mal posicionada).
        if (analise.SemCapacete == 0 && analise.SemColete == 0 && analise.SemOculos == 0
            && analise.CapaceteNaoAvaliavel + analise.ColeteNaoAvaliavel + analise.OculosNaoAvaliavel > 0)
            camera.NaoAvaliaveisNoDia++;
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
