using AAHBRANT.SST.Application.PerfisAcesso;
using AAHBRANT.SST.Application.PerfisAcesso.Commands;
using AAHBRANT.SST.Application.PerfisAcesso.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerfisAcessoController : ControllerBase
{
    private readonly IMediator _mediator;

    public PerfisAcessoController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "perfilacesso:ver")]
    [HttpGet]
    public async Task<ActionResult<List<PerfilAcessoDto>>> Listar()
    {
        var resultado = await _mediator.Send(new ListarPerfisAcessoQuery());
        return Ok(resultado);
    }

    [Authorize(Policy = "perfilacesso:ver")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PerfilAcessoDto>> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterPerfilAcessoPorIdQuery(id));
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [Authorize(Policy = "perfilacesso:criar")]
    [HttpPost]
    public async Task<ActionResult<Guid>> Criar(CriarPerfilAcessoCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [Authorize(Policy = "perfilacesso:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarPerfilAcessoCommand command)
    {
        if (id != command.Id) return BadRequest();
        await _mediator.Send(command);
        return NoContent();
    }

    [Authorize(Policy = "perfilacesso:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        await _mediator.Send(new ExcluirPerfilAcessoCommand(id));
        return NoContent();
    }

    [Authorize(Policy = "perfilacesso:ver")]
    [HttpGet("{id:guid}/permissoes")]
    public async Task<ActionResult<List<PerfilAcessoPermissaoDto>>> ListarPermissoes(Guid id)
    {
        var resultado = await _mediator.Send(new ListarPermissoesPorPerfilQuery(id));
        return Ok(resultado);
    }

    [Authorize(Policy = "perfilacesso:gerenciar_permissoes")]
    [HttpPut("{id:guid}/permissoes")]
    public async Task<IActionResult> DefinirPermissoes(Guid id, [FromBody] List<ItemPermissaoPerfil> itens)
    {
        await _mediator.Send(new DefinirPermissoesPerfilCommand(id, itens));
        return NoContent();
    }
}
