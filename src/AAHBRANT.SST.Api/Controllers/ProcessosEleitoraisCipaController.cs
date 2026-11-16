using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessosEleitoraisCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProcessosEleitoraisCipaController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "cipa:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarProcessosEleitoraisCipaQuery(obraId), ct));

    [Authorize(Policy = "cipa:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterProcessoEleitoralCipaDetalheQuery(id), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarProcessoEleitoralCipaCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirProcessoEleitoralCipaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/candidatos")]
    public async Task<IActionResult> Inscrever(Guid id, InscreverCandidatoCipaRequestBody body, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(new InscreverCandidatoCipaCommand(id, body.TrabalhadorId), ct) });

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("candidatos/{candidatoId:guid}/avaliar")]
    public async Task<IActionResult> AvaliarInscricao(Guid candidatoId, AvaliarInscricaoCandidatoCipaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AvaliarInscricaoCandidatoCipaCommand(candidatoId, body.Deferido, body.MotivoIndeferimento), ct);
        return NoContent();
    }

    [Authorize(Policy = "cipa:gerenciar")]
    [HttpPost("{id:guid}/apuracao")]
    public async Task<IActionResult> Apurar(Guid id, RegistrarApuracaoProcessoEleitoralCipaRequestBody body, CancellationToken ct)
    {
        var membrosIds = await _mediator.Send(new RegistrarApuracaoProcessoEleitoralCipaCommand(
            id, body.Votos, body.DataInicioMandato, body.DataFimMandato), ct);
        return Ok(new { membrosIds });
    }

    [Authorize(Policy = "cipa:exportar")]
    [HttpGet("{id:guid}/ata-pdf")]
    public async Task<IActionResult> ExportarAta(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarAtaEleicaoCipaPdfQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"ata-eleicao-cipa-{id}.pdf");
    }
}

// ---------- Urna biométrica (16/11/2026) ----------
// O tablet do canteiro roda logado como o técnico/mesário (cipa:gerenciar). A identidade do ELEITOR
// é provada pela biometria em cada voto (ver UrnaCipaCommands.cs).
[ApiController]
[Route("api/processoseleitoraiscipa/{id:guid}/urna")]
[Authorize(Policy = "cipa:gerenciar")]
public class UrnaCipaController : ControllerBase
{
    private readonly IMediator _mediator;
    public UrnaCipaController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var urna = await _mediator.Send(new ObterUrnaCipaQuery(id), ct);
        return urna is null ? NotFound() : Ok(urna);
    }

    [HttpGet("candidatos/{candidatoId:guid}/foto")]
    public async Task<IActionResult> Foto(Guid id, Guid candidatoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoCandidatoUrnaCipaQuery(id, candidatoId), ct);
        return foto is null ? NotFound() : File(foto.Value.Conteudo, foto.Value.ContentType);
    }

    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir(Guid id, CancellationToken ct)
        => Ok(new { totalEleitoresAptos = await _mediator.Send(new AbrirVotacaoCipaCommand(id), ct) });

    [HttpPost("encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new EncerrarVotacaoCipaCommand(id), ct));

    [HttpPost("identificar/digital")]
    public Task<IActionResult> IdentificarDigital(Guid id, IdentificarEleitorDigitalCipaRequestBody body, CancellationToken ct)
        => Cedula(() => _mediator.Send(new IdentificarEleitorDigitalCipaCommand(id, body.TrabalhadorId, body.DispositivoId,
            body.SegredoDispositivo, body.Score, body.Desafio, body.AssinaturaLeitura), ct));

    [HttpPost("identificar/facial")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> IdentificarFacial(Guid id, [FromForm] IdentificarEleitorFacialCipaRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        var foto = stream.ToArray();
        try
        {
            return await Cedula(() => _mediator.Send(new IdentificarEleitorFacialCipaCommand(id, foto), ct));
        }
        catch (AAHBRANT.SST.Application.Assinatura.Commands.RejeicaoFacialException ex)
        {
            return BadRequest(new { erro = ex.Message, motivo = ex.Motivo.ToString() });
        }
    }

    [HttpPost("identificar/mesario")]
    public Task<IActionResult> LiberarMesario(Guid id, LiberarEleitorMesarioCipaRequestBody body, CancellationToken ct)
        => Cedula(() => _mediator.Send(new LiberarEleitorMesarioCipaCommand(id, body.TrabalhadorId), ct));

    [HttpPost("votar")]
    public async Task<IActionResult> Votar(Guid id, VotarCipaRequestBody body, CancellationToken ct)
        => Ok(new { totalVotantes = await _mediator.Send(new VotarCipaCommand(id, body.CedulaId, body.CandidatoId), ct) });

    // "Já votou" não é erro de sistema: volta 409 com o horário para a tela mostrar.
    private static async Task<IActionResult> Cedula(Func<Task<CedulaCipaDto>> emitir)
    {
        try
        {
            return new OkObjectResult(await emitir());
        }
        catch (JaVotouCipaException ex)
        {
            return new ConflictObjectResult(new { erro = ex.Message, jaVotou = true, nome = ex.Nome, votouEmUtc = ex.VotouEmUtc });
        }
    }
}

public class IdentificarEleitorDigitalCipaRequestBody
{
    public Guid TrabalhadorId { get; set; }
    public Guid DispositivoId { get; set; }
    public string SegredoDispositivo { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? Desafio { get; set; }
    public string? AssinaturaLeitura { get; set; }
}

public class IdentificarEleitorFacialCipaRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

public record LiberarEleitorMesarioCipaRequestBody(Guid TrabalhadorId);
public record VotarCipaRequestBody(Guid CedulaId, Guid CandidatoId);

public record InscreverCandidatoCipaRequestBody(Guid TrabalhadorId);
public record AvaliarInscricaoCandidatoCipaRequestBody(bool Deferido, string? MotivoIndeferimento);
public record RegistrarApuracaoProcessoEleitoralCipaRequestBody(List<VotoApuradoCipa> Votos, DateTime DataInicioMandato, DateTime DataFimMandato);
