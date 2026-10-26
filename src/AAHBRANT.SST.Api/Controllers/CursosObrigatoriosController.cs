using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.CursosObrigatorios;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Cursos obrigatórios por função a partir do PGR (21/10). A obra mostra as sugestões; aprovar ou
// recusar é do Engenheiro de Segurança; as regras risco → NR → curso são do QSMS (treinamento:editar).
[ApiController]
[Route("api/cursos-obrigatorios")]
public class CursosObrigatoriosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public CursosObrigatoriosController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("obras/{obraId:guid}")]
    public async Task<IActionResult> Listar(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCursosObrigatoriosObraQuery(obraId), ct));

    [Authorize(Policy = "treinamento:editar")]
    [HttpPost("obras/{obraId:guid}/gerar")]
    public async Task<IActionResult> Gerar(Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new GerarCursosObrigatoriosObraCommand(obraId), ct));

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("obras/{obraId:guid}/aprovar-sugestoes")]
    public async Task<IActionResult> AprovarSugestoes(Guid obraId, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var aprovados = await _mediator.Send(new AprovarSugestoesCursosObrigatoriosCommand(obraId, usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { aprovados });
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("obras/{obraId:guid}")]
    public async Task<IActionResult> Adicionar(Guid obraId, AdicionarCursoObrigatorioRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var id = await _mediator.Send(new AdicionarCursoObrigatorioCommand(obraId, corpo.FuncaoId, corpo.CursoTreinamentoId,
            usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("{id:guid}/decidir")]
    public async Task<IActionResult> Decidir(Guid id, DecidirCursoObrigatorioRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new DecidirCursoObrigatorioCommand(id, corpo.Aprovar, corpo.Justificativa, usuarioId, User.FindFirst("name")?.Value), ct);
        return NoContent();
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("regras")]
    public async Task<IActionResult> ListarRegras(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRegrasCursoObrigatorioQuery(), ct));

    [Authorize(Policy = "treinamento:editar")]
    [HttpPost("regras")]
    public async Task<IActionResult> SalvarRegra(SalvarRegraCursoObrigatorioCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });
}

public class AdicionarCursoObrigatorioRequest
{
    public Guid FuncaoId { get; set; }
    public Guid CursoTreinamentoId { get; set; }
}

public class DecidirCursoObrigatorioRequest
{
    public bool Aprovar { get; set; }
    public string? Justificativa { get; set; }
}
