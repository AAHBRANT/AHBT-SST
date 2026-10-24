using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Commands;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Termo de Recebimento e Compromisso de Uso do EPI, um por funcionário (03/10). A assinatura digital
// passa pelo Motor de Assinatura (/api/documentos, EntidadeTipo "TermoCompromissoEpi"); aqui ficam a
// situação do termo e o registro de quem JÁ assinou em papel.
[ApiController]
[Route("api/[controller]")]
public class TermosCompromissoEpiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public TermosCompromissoEpiController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "epi:ver")]
    [HttpGet("{trabalhadorId:guid}")]
    public async Task<IActionResult> Obter(Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterTermoCompromissoEpiQuery(trabalhadorId), ct));

    // Registra que o funcionário já assinou o termo em papel. Foto/PDF opcional.
    [Authorize(Policy = "epi:editar")]
    [HttpPost("{trabalhadorId:guid}/manual")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> RegistrarManual(Guid trabalhadorId, [FromForm] RegistrarTermoManualRequestBody body, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);

        string? nomeArquivo = null;
        string? contentType = null;
        byte[]? conteudo = null;
        if (body.Arquivo is { Length: > 0 })
        {
            using var stream = new MemoryStream();
            await body.Arquivo.CopyToAsync(stream, ct);
            conteudo = stream.ToArray();
            nomeArquivo = body.Arquivo.FileName;
            contentType = body.Arquivo.ContentType;
        }

        var id = await _mediator.Send(new RegistrarTermoManualEpiCommand(
            trabalhadorId, body.DataAssinaturaPapel, body.Observacao, usuarioId, nomeArquivo, conteudo, contentType), ct);
        return Ok(new { id });
    }

    // inline=true abre no visualizador do navegador; sem o parâmetro, baixa.
    [Authorize(Policy = "epi:ver")]
    [HttpGet("{trabalhadorId:guid}/manual/arquivo")]
    public async Task<IActionResult> ObterArquivoManual(Guid trabalhadorId, [FromQuery] bool inline, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoTermoManualEpiQuery(trabalhadorId), ct);
        if (arquivo is null) return NotFound();
        return inline
            ? File(arquivo.Conteudo, arquivo.ContentType)
            : File(arquivo.Conteudo, arquivo.ContentType, arquivo.Nome);
    }

    // Remover o registro manual é privilégio de Administrador (mesma regra das demais exclusões).
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpDelete("{trabalhadorId:guid}/manual")]
    public async Task<IActionResult> RemoverManual(Guid trabalhadorId, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new RemoverTermoManualEpiCommand(trabalhadorId, usuarioId), ct);
        return NoContent();
    }
}

public class RegistrarTermoManualRequestBody
{
    public DateTime DataAssinaturaPapel { get; set; }
    public string? Observacao { get; set; }
    public IFormFile? Arquivo { get; set; }
}
