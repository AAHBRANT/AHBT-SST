using System.Security.Claims;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Plataforma;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/plataforma")]
[Authorize(Policy = PoliticasAutorizacao.QualquerUsuarioAutenticado)]
public sealed class PlataformaController(IMediator mediator) : ControllerBase
{
    private string? ObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("modulos/{modulo}/obras")]
    public async Task<IActionResult> Obras(string modulo, [FromQuery] bool configurarTeams, CancellationToken ct)
        => Ok(await mediator.Send(new ListarObrasModuloQuery(ObjectId, modulo, configurarTeams), ct));

    [HttpGet("modulos/{modulo}/obras/{obraId:guid}")]
    public async Task<IActionResult> Contexto(string modulo, Guid obraId, CancellationToken ct)
        => Ok(await mediator.Send(new ObterContextoModuloQuery(ObjectId, modulo, obraId), ct));

    // Somente valida e monta o destino. A gravação da configuração da aba é feita pelo TeamsJS
    // no host Teams. A URL/obra recebida do navegador nunca é prova de autorização.
    [HttpGet("abas/{modulo}/{obraId:guid}")]
    [Authorize(Policy = ModulosPlataforma.TeamsConfigurar)]
    public async Task<IActionResult> Aba(string modulo, Guid obraId, CancellationToken ct)
    {
        var contexto = await mediator.Send(new ObterContextoModuloQuery(ObjectId, modulo, obraId, true), ct);
        return Ok(new
        {
            nome = $"{(modulo == "qualidade" ? "Qualidade" : "SST")} · {contexto.Obra.Codigo}",
            caminho = $"/{(modulo == "qualidade" ? "qualidade" : "modulos/sst")}?obraId={obraId:D}",
        });
    }
}
