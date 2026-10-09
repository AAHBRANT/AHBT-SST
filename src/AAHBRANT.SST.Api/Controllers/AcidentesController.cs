using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Acidentes.Queries;
using AAHBRANT.SST.Application.SuporteIa;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcidentesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITranscricaoAudioService _transcricao;
    private readonly ILogger<AcidentesController> _logger;

    // gpt-4o-transcribe aceita até 25 MB por arquivo.
    private const long TamanhoMaximoAudio = 25_000_000;

    public AcidentesController(IMediator mediator, ITranscricaoAudioService transcricao, ILogger<AcidentesController> logger)
    {
        _mediator = mediator;
        _transcricao = transcricao;
        _logger = logger;
    }

    // Relato da ocorrência escrito pelo técnico → sugestão de preenchimento do formulário (IA).
    // Nada é gravado: o técnico revisa e registra pelo POST normal.
    [Authorize(Policy = "acidente:criar")]
    [HttpPost("relato-texto")]
    public async Task<IActionResult> RelatoTexto(RelatoTextoOcorrenciaBody body, CancellationToken ct)
        => Ok(new
        {
            transcricao = body.Relato,
            sugestao = await _mediator.Send(new ClassificarRelatoOcorrenciaCommand(body.Relato, body.ObraId, body.Complementos), ct),
        });

    // Só transcreve (sem classificar): usado em "Responder por voz" às perguntas da IA — o texto vira
    // complemento do relato na rodada seguinte de relato-texto.
    [Authorize(Policy = "acidente:criar")]
    [HttpPost("transcrever")]
    [RequestSizeLimit(TamanhoMaximoAudio + 1_000_000)]
    public async Task<IActionResult> Transcrever([FromForm] RelatoVozOcorrenciaBody body, CancellationToken ct)
    {
        if (!_transcricao.Configurado)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Transcrição de áudio ainda não está configurada neste ambiente." });
        if (body.Audio is null || body.Audio.Length == 0)
            return BadRequest(new { erro = "Nenhum áudio recebido." });
        if (body.Audio.Length > TamanhoMaximoAudio)
            return BadRequest(new { erro = "Áudio muito longo. Grave até cerca de 10 minutos." });

        await using var stream = body.Audio.OpenReadStream();
        var nomeArquivo = string.IsNullOrWhiteSpace(body.Audio.FileName) ? "resposta.webm" : body.Audio.FileName;
        return Ok(new { transcricao = await _transcricao.TranscreverAsync(stream, nomeArquivo, body.Audio.ContentType, ct) });
    }

    // Relato falado: transcreve o áudio (não é salvo) e sugere o preenchimento. Se a classificação
    // falhar, devolve só a transcrição para o técnico completar o formulário.
    [Authorize(Policy = "acidente:criar")]
    [HttpPost("relato-voz")]
    [RequestSizeLimit(TamanhoMaximoAudio + 1_000_000)]
    public async Task<IActionResult> RelatoVoz([FromForm] RelatoVozOcorrenciaBody body, CancellationToken ct)
    {
        if (!_transcricao.Configurado)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Transcrição de áudio ainda não está configurada neste ambiente." });
        if (body.ObraId == Guid.Empty)
            return BadRequest(new { erro = "Selecione a obra antes de relatar a ocorrência." });
        if (body.Audio is null || body.Audio.Length == 0)
            return BadRequest(new { erro = "Nenhum áudio recebido." });
        if (body.Audio.Length > TamanhoMaximoAudio)
            return BadRequest(new { erro = "Áudio muito longo. Grave até cerca de 10 minutos." });

        await using var stream = body.Audio.OpenReadStream();
        var nomeArquivo = string.IsNullOrWhiteSpace(body.Audio.FileName) ? "relato.webm" : body.Audio.FileName;
        var transcricao = await _transcricao.TranscreverAsync(stream, nomeArquivo, body.Audio.ContentType, ct);
        if (string.IsNullOrWhiteSpace(transcricao))
            return Ok(new { transcricao, sugestao = (RelatoOcorrenciaSugestaoDto?)null });

        RelatoOcorrenciaSugestaoDto? sugestao = null;
        try
        {
            sugestao = await _mediator.Send(new ClassificarRelatoOcorrenciaCommand(transcricao, body.ObraId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Relato de ocorrência transcrito, mas a classificação falhou; devolvendo só a transcrição.");
        }

        return Ok(new { transcricao, sugestao });
    }

    [Authorize(Policy = "acidente:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] TipoOcorrencia? tipo,
        [FromQuery] StatusAcidente? status,
        [FromQuery] Guid? obraId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAcidentesQuery(tipo, status, obraId), ct));

    [Authorize(Policy = "acidente:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterAcidenteDetalheQuery(id), ct));

    [Authorize(Policy = "acidente:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAcidenteCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAcidenteRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarAcidenteCommand(
            id, body.Tipo, body.ObraId, body.TrabalhadorId, body.AtividadeId, body.Local, body.Data,
            body.Hora, body.Descricao, body.Lesao, body.Consequencia, body.Atendimento,
            body.HouveAfastamento, body.DiasAfastamento, body.NumeroCat, body.MetodologiaInvestigacao,
            body.Causas, body.Gravidade, body.DiasDebitadosInformados, body.TrabalhadoresIds), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirAcidenteCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:avancar_status")]
    [HttpPost("{id:guid}/avancar-status")]
    public async Task<IActionResult> AvancarStatus(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new AvancarStatusAcidenteCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpPost("{id:guid}/fotos")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid id, [FromForm] AnexarFotoAcidenteBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        var fotoId = await _mediator.Send(new AnexarFotoAcidenteCommand(id, body.Ordem, stream.ToArray(), body.Foto.ContentType, body.Metadados), ct);
        return Ok(new { id = fotoId });
    }

    [Authorize(Policy = "acidente:ver")]
    [HttpGet("fotos/{fotoId:guid}")]
    public async Task<IActionResult> ObterFoto(Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoAcidenteQuery(fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType);
    }

    [Authorize(Policy = "acidente:editar")]
    [HttpDelete("fotos/{fotoId:guid}")]
    public async Task<IActionResult> RemoverFoto(Guid fotoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverFotoAcidenteCommand(fotoId), ct);
        return NoContent();
    }
}

public class AnexarFotoAcidenteBody
{
    public IFormFile Foto { get; set; } = null!;
    public int Ordem { get; set; }
    public string? Metadados { get; set; }
}

public record AtualizarAcidenteRequestBody(
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados,
    List<Guid>? TrabalhadoresIds = null);

public class RelatoTextoOcorrenciaBody
{
    public Guid ObraId { get; set; }
    public string Relato { get; set; } = string.Empty;
    // Respostas às perguntas da IA (rodada "Completar com as respostas").
    public List<RespostaPerguntaRelato>? Complementos { get; set; }
}

public class RelatoVozOcorrenciaBody
{
    public Guid ObraId { get; set; }
    public IFormFile? Audio { get; set; }
}
