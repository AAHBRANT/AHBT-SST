using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AAHBRANT.SST.Api.Autorizacao;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TreinamentosController : ControllerBase
{
    private readonly IMediator _mediator;

    public TreinamentosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? trabalhadorId, [FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTreinamentosQuery(trabalhadorId, obraId), ct));

    // Lista central da sub-aba "Certificados" (22/09) — já traz trabalhador, função, obra e curso
    // resolvidos, para o técnico lançar e conferir certificados sem entrar perfil por perfil.
    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("certificados")]
    public async Task<IActionResult> ListarCertificados(
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? cursoTreinamentoId,
        [FromQuery] Guid? trabalhadorId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCertificadosTreinamentoQuery(obraId, cursoTreinamentoId, trabalhadorId), ct));

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var treinamento = await _mediator.Send(new ObterTreinamentoPorIdQuery(id), ct);
        return treinamento is null ? NotFound() : Ok(treinamento);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTreinamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarTreinamentoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // Exclusao definitiva e privilegio de Administrador (pedido do usuario, 23/09): quem tem
    // permissao de editar corrige o registro, mas nao o apaga. Ver PoliticasAutorizacao.
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTreinamentoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}/certificado/pdf")]
    public async Task<IActionResult> ExportarCertificado(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new ExportarCertificadoTreinamentoQuery(id), ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"certificado-treinamento-{id}.pdf");
    }

    // Certificado digitalizado (22/09): PDF original ou foto do papel, para o lançamento retroativo
    // de treinamentos feitos antes de a obra entrar no sistema. Um arquivo por treinamento — reenviar
    // substitui o anterior. Mesmo padrão multipart de SessoesTreinamentoController.AnexarFotoEvidencia.
    [Authorize(Policy = "treinamento:editar")]
    [HttpPost("{id:guid}/certificado/arquivo")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> AnexarArquivoCertificado(Guid id, [FromForm] AnexarArquivoCertificadoRequestBody body, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);
        var arquivoId = await _mediator.Send(new AnexarArquivoCertificadoTreinamentoCommand(
            id, body.Arquivo.FileName, stream.ToArray(), body.Arquivo.ContentType), ct);
        return Ok(new { id = arquivoId });
    }

    // inline=true abre o arquivo no visualizador do navegador (o técnico só quer conferir); sem o
    // parâmetro, o navegador baixa.
    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}/certificado/arquivo")]
    public async Task<IActionResult> ObterArquivoCertificado(Guid id, [FromQuery] bool inline, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoCertificadoTreinamentoQuery(id), ct);
        if (arquivo is null) return NotFound();
        return inline
            ? File(arquivo.Conteudo, arquivo.ContentType)
            : File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    [Authorize(Policy = "treinamento:editar")]
    [HttpDelete("{id:guid}/certificado/arquivo")]
    public async Task<IActionResult> RemoverArquivoCertificado(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RemoverArquivoCertificadoTreinamentoCommand(id), ct);
        return NoContent();
    }
}

public class AnexarArquivoCertificadoRequestBody
{
    public IFormFile Arquivo { get; set; } = null!;
}
