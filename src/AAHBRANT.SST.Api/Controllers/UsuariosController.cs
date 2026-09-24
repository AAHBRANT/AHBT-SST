using System.Security.Claims;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Usuarios;
using AAHBRANT.SST.Application.Usuarios.Commands;
using AAHBRANT.SST.Application.Usuarios.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuracao;

    public UsuariosController(IMediator mediator, IConfiguration configuracao)
    {
        _mediator = mediator;
        _configuracao = configuracao;
    }

    // "Quem sou eu": nome, se é Administrador e os códigos de permissão de quem está logado. A tela
    // usa isto para não oferecer ação que o servidor vai recusar — hoje, o botão de excluir
    // registro. A identidade sai do claim do token, nunca de parâmetro da requisição.
    [Authorize(Policy = PoliticasAutorizacao.QualquerUsuarioAutenticado)]
    [HttpGet("eu")]
    public async Task<ActionResult<UsuarioLogadoDto>> Eu(CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // Mesma flag de PermissaoAuthorizationHandler e EscopoPorObraMiddleware: com o Entra ID
        // desligado (desenvolvimento), o servidor não checa nada e a tela precisa saber disso.
        var autenticacaoHabilitada = !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
        return Ok(await _mediator.Send(new ObterUsuarioLogadoQuery(azureAdObjectId, autenticacaoHabilitada), ct));
    }

    [Authorize(Policy = "usuario:ver")]
    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar([FromQuery] StatusUsuario? status)
    {
        var resultado = await _mediator.Send(new ListarUsuariosQuery(status));
        return Ok(resultado);
    }

    [Authorize(Policy = "usuario:ver")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsuarioDto>> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterUsuarioPorIdQuery(id));
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [Authorize(Policy = "usuario:criar")]
    [HttpPost]
    public async Task<ActionResult<Guid>> Criar(CriarUsuarioCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [Authorize(Policy = "usuario:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarUsuarioCommand command)
    {
        if (id != command.Id) return BadRequest();
        await _mediator.Send(command);
        return NoContent();
    }

    [Authorize(Policy = "usuario:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        await _mediator.Send(new ExcluirUsuarioCommand(id));
        return NoContent();
    }

    [Authorize(Policy = "usuario:editar")]
    [HttpPost("perfis-obra")]
    public async Task<ActionResult<Guid>> AtribuirPerfilObra(AtribuirPerfilObraCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(id);
    }

    [Authorize(Policy = "usuario:excluir")]
    [HttpDelete("perfis-obra/{id:guid}")]
    public async Task<IActionResult> RemoverPerfilObra(Guid id)
    {
        await _mediator.Send(new RemoverPerfilObraCommand(id));
        return NoContent();
    }
}
