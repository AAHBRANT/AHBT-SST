using System.Security.Claims;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Instrutores;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Perfis de instrutor de treinamento (28/10). Listar usa a permissão do módulo; cadastrar e autorizar o
// uso em outras obras fica só com o Administrador, porque libera a digital do instrutor nos leitores das
// outras obras (dado biométrico sensível).
[ApiController]
[Route("api/[controller]")]
public class InstrutoresController : ControllerBase
{
    private readonly IMediator _mediator;

    public InstrutoresController(IMediator mediator) => _mediator = mediator;

    private string? AzureAdObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await _mediator.Send(new ListarPerfisInstrutorQuery(), ct));

    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpPut]
    public async Task<IActionResult> Salvar(SalvarPerfilInstrutorRequestBody body, CancellationToken ct)
    {
        var id = await _mediator.Send(new SalvarPerfilInstrutorCommand(
            body.TrabalhadorId, body.Formacao, body.Conselho, body.NumeroRegistro, body.UfRegistro,
            body.PodeAssinarEmOutrasObras, AzureAdObjectId), ct);
        return Ok(new { id });
    }
}

public record SalvarPerfilInstrutorRequestBody(
    Guid TrabalhadorId, FormacaoInstrutor Formacao, ConselhoInstrutor? Conselho, string? NumeroRegistro, string? UfRegistro,
    bool PodeAssinarEmOutrasObras);
