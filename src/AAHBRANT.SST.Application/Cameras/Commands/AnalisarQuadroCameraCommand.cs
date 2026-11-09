using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record AnalisarQuadroCameraCommand(Guid CameraId, byte[] Imagem, DateTime CapturadaEmUtc) : IRequest<ResultadoQuadroDto>;

public record ResultadoQuadroDto(string Situacao, Guid? OcorrenciaId, bool AlertaEnviado, int Pessoas, int SemCapacete, int SemColete);

public static class SituacaoQuadro
{
    public const string Ignorado = "ignorado";     // dentro do teto de frequência; nada foi analisado
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
    // O navegador envia no máximo um quadro a cada 30 s; 25 s tolera variação do temporizador.
    public static readonly TimeSpan IntervaloMinimoEntreAnalises = TimeSpan.FromSeconds(25);

    private readonly IAppDbContext _db;
    private readonly IAnalisadorEpiImagem _analisador;
    private readonly ITelegramResumoService _telegram;
    private readonly ILogger<AnalisarQuadroCameraCommandHandler> _logger;

    public AnalisarQuadroCameraCommandHandler(
        IAppDbContext db,
        IAnalisadorEpiImagem analisador,
        ITelegramResumoService telegram,
        ILogger<AnalisarQuadroCameraCommandHandler> logger)
    {
        _db = db;
        _analisador = analisador;
        _telegram = telegram;
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
        camera.UltimaAnaliseEmUtc = agora;
        await _db.SaveChangesAsync(ct);

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

        var podeAlertar = camera.IntervaloMinimoAlertasMinutos == 0
            || camera.UltimoAlertaEmUtc is null
            || agora - camera.UltimoAlertaEmUtc.Value >= TimeSpan.FromMinutes(camera.IntervaloMinimoAlertasMinutos);

        if (podeAlertar && await EnviarAlertaAsync(camera, ocorrencia, ct))
        {
            ocorrencia.AlertaEnviado = true;
            ocorrencia.AlertaEnviadoEmUtc = agora;
            camera.UltimoAlertaEmUtc = agora;
        }

        await _db.SaveChangesAsync(ct);
        return new ResultadoQuadroDto(SituacaoQuadro.Ocorrencia, ocorrencia.Id, ocorrencia.AlertaEnviado,
            analise.Pessoas, analise.SemCapacete, analise.SemColete);
    }

    // Falha no Telegram não pode impedir o registro: a ocorrência fica gravada com "alerta não enviado".
    private async Task<bool> EnviarAlertaAsync(Camera camera, OcorrenciaEpiCamera ocorrencia, CancellationToken ct)
    {
        try
        {
            var legenda = LegendaAlertaEpi.Montar(camera.Obra?.Nome ?? "-", camera.Nome, ocorrencia.RecebidaEmUtc,
                ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete);
            return await _telegram.EnviarFotoAsync(ocorrencia.Foto, legenda, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Alerta de EPI da câmera {CameraId} não enviado ao Telegram.", camera.Id);
            return false;
        }
    }
}
