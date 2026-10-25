using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.MatrizRisco.Commands;
using AAHBRANT.SST.Application.MatrizRisco.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Matriz de risco versionada (25/10/2026): a vigente só se consulta; mudar é criar uma nova versão, que substitui a
// anterior e deixa o histórico. Não há edição nem exclusão. Só o Engenheiro de Segurança cria versão.
[ApiController]
[Route("api/[controller]")]
public class MatrizRiscoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public MatrizRiscoController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    public record NovaVersaoRequest(
        string Nome,
        int NumNiveisProbabilidade,
        int NumNiveisSeveridade,
        List<NovaVersaoMatrizRiscoCelula> Celulas,
        string? Motivo);

    // Todas as versões, da mais nova (a vigente) para a mais antiga.
    [Authorize(Policy = "risco:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarMatrizRiscoConfigsQuery(), ct));

    // A matriz que classifica os riscos hoje. 204 enquanto a empresa não cadastrou a primeira.
    [Authorize(Policy = "risco:ver")]
    [HttpGet("vigente")]
    public async Task<IActionResult> ObterVigente(CancellationToken ct)
    {
        var vigente = await _mediator.Send(new ObterMatrizRiscoVigenteQuery(), ct);
        return vigente is null ? NoContent() : Ok(vigente);
    }

    [Authorize(Policy = "risco:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var config = await _mediator.Send(new ObterMatrizRiscoConfigPorIdQuery(id), ct);
        return config is null ? NotFound() : Ok(config);
    }

    // Cria a primeira matriz (versão 1) ou uma nova versão que substitui a vigente. Quem revisa e o motivo ficam registrados.
    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost]
    public async Task<IActionResult> NovaVersao(NovaVersaoRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var id = await _mediator.Send(new NovaVersaoMatrizRiscoCommand(
            corpo.Nome, corpo.NumNiveisProbabilidade, corpo.NumNiveisSeveridade, corpo.Celulas, corpo.Motivo,
            usuarioId, User.FindFirst("name")?.Value), ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }
}
