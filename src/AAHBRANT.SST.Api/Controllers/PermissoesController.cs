using AAHBRANT.SST.Application.Permissoes;
using AAHBRANT.SST.Application.Permissoes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Somente leitura: o catálogo de permissões é semeado na inicialização, não editável via API
// (ver aviso em ListarPermissoesQuery.cs).
[ApiController]
[Route("api/[controller]")]
public class PermissoesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissoesController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "perfilacesso:ver")]
    [HttpGet]
    public async Task<ActionResult<List<PermissaoDto>>> Listar([FromQuery] string? modulo)
    {
        var resultado = await _mediator.Send(new ListarPermissoesQuery(modulo));
        return Ok(resultado);
    }
}
