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
public record ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete,
    Guid? TrabalhadorId = null, string? TrabalhadorNome = null);

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

    private readonly IAppDbContext _db;
    private readonly IAnalisadorEpiImagem _analisador;
    // A identificação pelo rosto não pode segurar o alerta: passou disso, sai como "não identificado".
    public static readonly TimeSpan TempoMaximoIdentificacaoFacial = TimeSpan.FromSeconds(20);

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
        // Também conta a análise (custo por foto do piloto). Dois quadros simultâneos da mesma câmera
        // disputam a versão da linha: o perdedor é tratado como "ignorado", não como erro.
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
            return new ResultadoQuadroDto(SituacaoQuadro.Limpo, null, false, analise.Pessoas, 0, 0);

        // Várias pessoas na cena, com a posição de cada uma: identifica cada irregular pelo rosto.
        if (analise.Pessoas > 1 && analise.Detalhe is { Count: > 1 } detalhe && detalhe.Any(p => p.Irregular))
            return await RegistrarCenaComVariasPessoasAsync(camera, request, analise, detalhe, agora);

        var ocorrencia = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id,
            ObraId = camera.ObraId,
            CapturadaEmUtc = request.CapturadaEmUtc,
            RecebidaEmUtc = agora,
            Foto = request.Imagem,
            PessoasNaCena = analise.Pessoas,
            SemCapacete = analise.SemCapacete,
            SemColete = analise.SemColete,
            Confianca = analise.Confianca,
        };
        _db.OcorrenciasEpiCamera.Add(ocorrencia);

        // A ocorrência é gravada ANTES do alerta e sem o token da requisição: a chamada à IA pode demorar
        // minutos e um cancelamento do cliente ou conflito na câmera não pode perder a evidência.
        await _db.SaveChangesAsync(CancellationToken.None);

        // Uma única pessoa na cena: o rosto da foto é o dela. (Várias pessoas com posição conhecida seguem por
        // RegistrarCenaComVariasPessoasAsync; sem posição, ficam sem identificação.) Fica antes do alerta
        // para o nome sair na legenda.
        var trabalhadorNome = analise.Pessoas == 1 ? await IdentificarPeloRostoAsync(camera, ocorrencia) : null;

        bool podeAlertar;
        if (trabalhadorNome is not null && ocorrencia.TrabalhadorId is { } trabalhadorId)
        {
            // Trabalhador identificado: um alerta por pessoa a cada hora, em qualquer câmera (regra do usuário
            // em 09/11/2026). Vale no lugar do intervalo da câmera, para o alerta de um não segurar o de outro.
            var limite = agora - IntervaloAlertasPorTrabalhador;
            podeAlertar = !await _db.OcorrenciasEpiCamera.AnyAsync(
                o => o.TrabalhadorId == trabalhadorId && o.AlertaEnviado && o.AlertaEnviadoEmUtc >= limite,
                CancellationToken.None);
        }
        else
        {
            // Sem identificação não há como saber se é a mesma pessoa: vale o intervalo da câmera.
            podeAlertar = camera.IntervaloMinimoAlertasMinutos == 0
                || camera.UltimoAlertaEmUtc is null
                || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);
        }

        if (podeAlertar && await EnviarAlertaAsync(camera, ocorrencia, trabalhadorNome))
        {
            var enviadoEm = DateTime.UtcNow;
            ocorrencia.AlertaEnviado = true;
            ocorrencia.AlertaEnviadoEmUtc = enviadoEm;
            await _db.SaveChangesAsync(CancellationToken.None);

            // Se a câmera mudou durante a análise, só se perde o intervalo entre alertas desta vez.
            camera.UltimoAlertaEmUtc = enviadoEm;
            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Câmera {CameraId} alterada durante a análise; intervalo entre alertas não registrado.", camera.Id);
            }
        }

        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, ocorrencia.Id, ocorrencia.AlertaEnviado,
            analise.Pessoas, analise.SemCapacete, analise.SemColete,
            trabalhadorNome is null ? null : ocorrencia.TrabalhadorId, trabalhadorNome);
    }

    // Cena com várias pessoas. Cada irregular identificado pelo rosto ganha a própria ocorrência (com o que
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

        OcorrenciaEpiCamera Nova(int semCapacete, int semColete) => new()
        {
            CameraId = camera.Id,
            ObraId = camera.ObraId,
            CapturadaEmUtc = request.CapturadaEmUtc,
            RecebidaEmUtc = agora,
            Foto = request.Imagem,
            PessoasNaCena = analise.Pessoas,
            SemCapacete = semCapacete,
            SemColete = semColete,
            Confianca = analise.Confianca,
        };

        var identificadas = new List<(OcorrenciaEpiCamera Ocorrencia, IdentificadoNaCena Pessoa)>();
        var naoIdentificados = new List<PessoaNaCena>();
        var jaIncluidos = new HashSet<Guid>();
        for (var i = 0; i < detalhe.Count; i++)
        {
            var pessoa = detalhe[i];
            if (!pessoa.Irregular) continue;

            // O mesmo trabalhador não entra duas vezes na mesma foto, nem um que o banco não enxerga.
            if (rostoPorPessoa.TryGetValue(i, out var rosto) && rosto.TrabalhadorId is { } trabalhadorId
                && nomes.TryGetValue(trabalhadorId, out var nome) && jaIncluidos.Add(trabalhadorId))
            {
                var ocorrencia = Nova(pessoa.SemCapacete ? 1 : 0, pessoa.SemColete ? 1 : 0);
                ocorrencia.TrabalhadorId = trabalhadorId;
                ocorrencia.IdentificadoPorRosto = true;
                ocorrencia.ConfiancaIdentificacao = rosto.Confianca;
                ocorrencia.VinculadoEmUtc = agora;
                identificadas.Add((ocorrencia, new IdentificadoNaCena(nome, pessoa.SemCapacete, pessoa.SemColete)));
            }
            else
            {
                naoIdentificados.Add(pessoa);
            }
        }

        var semIdentificacao = naoIdentificados.Count == 0
            ? null
            : Nova(naoIdentificados.Count(p => p.SemCapacete), naoIdentificados.Count(p => p.SemColete));

        _db.OcorrenciasEpiCamera.AddRange(identificadas.Select(i => i.Ocorrencia));
        if (semIdentificacao is not null) _db.OcorrenciasEpiCamera.Add(semIdentificacao);
        // Gravadas antes do alerta e sem o token da requisição, como na cena de uma pessoa só.
        await _db.SaveChangesAsync(CancellationToken.None);

        // Quem já foi notificado na última hora (em qualquer câmera) fica fora do alerta desta foto.
        var limite = agora - IntervaloAlertasPorTrabalhador;
        var notificadosNaHora = ids.Count == 0
            ? new List<Guid>()
            : await _db.OcorrenciasEpiCamera
                .Where(o => o.TrabalhadorId != null && ids.Contains(o.TrabalhadorId.Value) && o.AlertaEnviado && o.AlertaEnviadoEmUtc >= limite)
                .Select(o => o.TrabalhadorId!.Value).Distinct().ToListAsync(CancellationToken.None);
        var aAlertar = identificadas.Where(i => !notificadosNaHora.Contains(i.Ocorrencia.TrabalhadorId!.Value)).ToList();

        // Sem identificação vale o intervalo da câmera, como na cena de uma pessoa só.
        var alertaSemIdentificacao = semIdentificacao is not null
            && (camera.IntervaloMinimoAlertasMinutos == 0
                || camera.UltimoAlertaEmUtc is null
                || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos));

        var totalSemCapacete = detalhe.Count(p => p.SemCapacete);
        var totalSemColete = detalhe.Count(p => p.SemColete);
        var alertaEnviado = false;
        if (aAlertar.Count > 0 || alertaSemIdentificacao)
        {
            var legenda = LegendaAlertaEpi.MontarCena(camera.Obra?.Nome ?? "-", camera.Nome, agora, analise.Pessoas,
                totalSemCapacete, totalSemColete, aAlertar.Select(i => i.Pessoa).ToList(),
                alertaSemIdentificacao ? naoIdentificados.Count : 0);
            alertaEnviado = await EnviarLegendaAsync(camera, request.Imagem, legenda);
        }

        if (alertaEnviado)
        {
            var enviadoEm = DateTime.UtcNow;
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

            camera.UltimoAlertaEmUtc = enviadoEm;
            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Câmera {CameraId} alterada durante a análise; intervalo entre alertas não registrado.", camera.Id);
            }
        }

        var primeira = identificadas.Select(i => i.Ocorrencia).FirstOrDefault() ?? semIdentificacao!;
        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, primeira.Id, alertaEnviado,
            analise.Pessoas, totalSemCapacete, totalSemColete,
            identificadas.Count == 1 ? identificadas[0].Ocorrencia.TrabalhadorId : null,
            identificadas.Count == 0 ? null : string.Join(", ", identificadas.Select(i => i.Pessoa.Nome)));
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
    // (rosto não reconhecido, obra sem facial, sem consentimento, Azure fora do ar) deixa a ocorrência
    // sem trabalhador, para o técnico vincular à mão. Devolve o nome de quem foi identificado.
    private async Task<string?> IdentificarPeloRostoAsync(Camera camera, OcorrenciaEpiCamera ocorrencia)
    {
        try
        {
            using var limite = new CancellationTokenSource(TempoMaximoIdentificacaoFacial);
            var identificacao = await _facial.IdentificarSemRegistrarFalhaAsync(camera.ObraId, ocorrencia.Foto, limite.Token);
            if (!identificacao.Aceito || identificacao.Resultado is null)
            {
                // Sem isto, "não identificado" não diz por quê (ex.: o mesmo rosto cadastrado em duas pessoas
                // sai como RostoAmbiguo, e parecia defeito do reconhecimento).
                _logger.LogInformation("Rosto da ocorrência {OcorrenciaId} não identificado: {Motivo} (confiança {Confianca}).",
                    ocorrencia.Id, identificacao.Motivo, identificacao.Confianca);
                return null;
            }

            var trabalhador = await _db.Trabalhadores
                .Where(t => t.Id == identificacao.Resultado.TrabalhadorId)
                .Select(t => new { t.Id, t.Nome })
                .FirstOrDefaultAsync(CancellationToken.None);
            if (trabalhador is null) return null;

            ocorrencia.TrabalhadorId = trabalhador.Id;
            ocorrencia.IdentificadoPorRosto = true;
            ocorrencia.ConfiancaIdentificacao = identificacao.Resultado.Confianca;
            ocorrencia.VinculadoEmUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            return trabalhador.Nome;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identificação pelo rosto da ocorrência {OcorrenciaId} não concluída.", ocorrencia.Id);
            // Se a gravação falhou, o vínculo não pode sair no alerta nem ser gravado depois por tabela.
            ocorrencia.TrabalhadorId = null;
            ocorrencia.IdentificadoPorRosto = false;
            ocorrencia.ConfiancaIdentificacao = null;
            ocorrencia.VinculadoEmUtc = null;
            return null;
        }
    }

    // Falha no Telegram não pode impedir o registro: a ocorrência fica gravada com "alerta não enviado".
    private Task<bool> EnviarAlertaAsync(Camera camera, OcorrenciaEpiCamera ocorrencia, string? trabalhadorNome)
        => EnviarLegendaAsync(camera, ocorrencia.Foto, LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome,
            ocorrencia.RecebidaEmUtc, ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete, trabalhadorNome));

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
