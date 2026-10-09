using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AAHBRANT.SST.Api.Autorizacao;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DdsController : ControllerBase
{
    private readonly IMediator _mediator;

    private readonly ILogger<DdsController> _logger;

    public DdsController(IMediator mediator, ILogger<DdsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDdsQuery(obraId), ct));

    // Temas agendados para o DDS da obra no dia (ex.: gerado por uma ocorrência da véspera).
    [Authorize(Policy = "dds:ver")]
    [HttpGet("temas-agendados")]
    public async Task<IActionResult> TemasAgendados([FromQuery] Guid obraId, [FromQuery] DateTime data, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTemasDdsAgendadosQuery(obraId, data), ct));

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDdsCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "dds:criar")]
    [HttpPost("sem-expediente")]
    public async Task<IActionResult> RegistrarSemExpediente(RegistrarDiaSemExpedienteCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("itens/{itemId:guid}/marcar")]
    public async Task<IActionResult> MarcarItem(Guid itemId, MarcarItemChecklistRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new MarcarItemChecklistCommand(itemId, body.Verificado), ct);
        return NoContent();
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/participantes")]
    public async Task<IActionResult> RegistrarParticipante(Guid id, RegistrarParticipanteRequestBody body, CancellationToken ct)
    {
        var command = new RegistrarParticipanteCommand(id, body.TrabalhadorId, body.DispositivoId, body.SegredoDispositivo, body.Score, body.ImagemDigital);
        var participanteId = await _mediator.Send(command, ct);
        return Ok(new { id = participanteId });
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/participantes/facial")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> RegistrarParticipanteFacial(Guid id, [FromForm] RegistrarParticipanteFacialRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        try
        {
            var participanteId = await _mediator.Send(new RegistrarParticipanteFacialCommand(id, body.TrabalhadorId, stream.ToArray()), ct);
            return Ok(new { id = participanteId });
        }
        catch (AAHBRANT.SST.Application.Assinatura.Commands.RejeicaoFacialException ex)
        {
            return BadRequest(new { erro = ex.Message, motivo = ex.Motivo.ToString() });
        }
    }

    // Fila facial: câmera aberta, cada funcionário olha e o Azure descobre quem é (1:N). Só a foto é
    // enviada; a obra vem do DDS.
    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/participantes/facial-fila")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> RegistrarParticipanteFacialFila(Guid id, [FromForm] RegistrarParticipanteFacialFilaRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        try
        {
            return Ok(await _mediator.Send(new RegistrarParticipanteFacialFilaCommand(id, stream.ToArray()), ct));
        }
        catch (AAHBRANT.SST.Application.Assinatura.Commands.RejeicaoFacialException ex)
        {
            return BadRequest(new { erro = ex.Message, motivo = ex.Motivo.ToString() });
        }
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}/funcionarios-disponiveis")]
    public async Task<IActionResult> ListarFuncionarios(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarFuncionariosDdsQuery(id), ct));

    [Authorize(Policy = "dds:conduzir")]
    [HttpPut("{id:guid}/funcionarios-selecionados")]
    public async Task<IActionResult> AtualizarFuncionarios(Guid id, AtualizarFuncionariosDdsRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarFuncionariosDdsCommand(id, body.TrabalhadoresIds), ct);
        return Ok(await _mediator.Send(new ObterDdsDetalheQuery(id), ct));
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("participantes/{participanteId:guid}/foto")]
    public async Task<IActionResult> ObterFotoParticipante(Guid participanteId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoParticipanteQuery(participanteId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "dds:encerrar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EncerrarDdsCommand(id), ct);

        // Resumo no Telegram: melhor esforço, com tempo limite curto. O DDS já foi encerrado e nenhuma
        // falha do Telegram pode devolver erro ao operador.
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(8));
            await _mediator.Send(new EnviarResumoDdsTelegramCommand(id), limite.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resumo do DDS {DdsId} não foi enviado ao Telegram.", id);
        }

        return NoContent();
    }

    // Evidências fotográficas obrigatórias do registro diário (31/08, pedido do usuário: "3 fotos
    // por registro de DDS para liberação do encerramento") — mesmo padrão de
    // InspecoesController.AnexarFoto/ObterFoto.
    [Authorize(Policy = "dds:conduzir")]
    [HttpPost("{id:guid}/fotos-evidencia")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFotoEvidencia(Guid id, [FromForm] AnexarFotoEvidenciaDdsRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var fotoId = await _mediator.Send(new AnexarFotoEvidenciaDdsCommand(id, body.Ordem, stream.ToArray(), body.Foto.ContentType, body.Metadados), ct);
        return Ok(new { id = fotoId });
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("fotos-evidencia/{fotoId:guid}")]
    public async Task<IActionResult> ObterFotoEvidencia(Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoEvidenciaDdsQuery(fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "dds:conduzir")]
    [HttpDelete("fotos-evidencia/{fotoId:guid}")]
    public async Task<IActionResult> RemoverFotoEvidencia(Guid fotoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverFotoEvidenciaDdsCommand(fotoId), ct);
        return NoContent();
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarDdsPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"dds-{id}.pdf");
    }
}

public record MarcarItemChecklistRequestBody(bool Verificado);
public record AtualizarFuncionariosDdsRequestBody(List<Guid> TrabalhadoresIds);

public class RegistrarParticipanteFacialFilaRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

public class RegistrarParticipanteFacialRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public IFormFile Foto { get; set; } = null!;
}

public class RegistrarParticipanteRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public double Score { get; set; }
    // PNG da digital lida (base64 no JSON), guardado como evidência visual da assinatura.
    public byte[]? ImagemDigital { get; set; }
}

public class AnexarFotoEvidenciaDdsRequestBody
{
    public string? Metadados { get; set; }
    public IFormFile Foto { get; set; } = null!;
    public int Ordem { get; set; }
}
