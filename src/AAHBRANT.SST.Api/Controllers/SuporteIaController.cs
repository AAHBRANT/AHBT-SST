using AAHBRANT.SST.Application.SuporteIa.Commands;
using AAHBRANT.SST.Application.SuporteIa.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/suporte-ia")]
public class SuporteIaController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _configuracao;

    public SuporteIaController(IMediator mediator, IAppDbContext db, IConfiguration configuracao)
    {
        _mediator = mediator;
        _db = db;
        _configuracao = configuracao;
    }

    [Authorize(Policy = "suporte-ia:usar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarSuporteIaRequestBody body, CancellationToken ct)
    {
        var solicitante = await ResolverSolicitanteAsync(body.SolicitanteEmail, ct);

        var resultado = await _mediator.Send(new CriarSolicitacaoSuporteIaCommand(
            body.Tipo,
            body.SeveridadeInformada,
            body.Titulo,
            body.Descricao,
            body.Modulo,
            body.UrlContexto,
            solicitante.UsuarioId,
            solicitante.Nome ?? body.SolicitanteNome,
            solicitante.Email), ct);

        return CreatedAtAction(nameof(Listar), new { id = resultado.Id }, resultado);
    }

    [Authorize(Policy = "suporte-ia:usar")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] StatusSolicitacaoSuporteIa? status, CancellationToken ct)
    {
        var solicitante = await ResolverSolicitanteAsync(null, ct);
        return Ok(await _mediator.Send(new ListarSolicitacoesSuporteIaQuery(
            status,
            solicitante.UsuarioId,
            solicitante.Email,
            IncluirTodos: false), ct));
    }

    [Authorize(Policy = "suporte-ia:administrar")]
    [HttpGet("admin")]
    public async Task<IActionResult> ListarTodos([FromQuery] StatusSolicitacaoSuporteIa? status, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarSolicitacoesSuporteIaQuery(status, IncluirTodos: true), ct));

    private async Task<SolicitanteAtual> ResolverSolicitanteAsync(string? emailFallback, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var email = User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst("email")?.Value
            ?? emailFallback;

        var nome = User.Identity?.Name
            ?? User.FindFirst("name")?.Value
            ?? email;

        if (!string.IsNullOrWhiteSpace(azureAdObjectId))
        {
            var usuario = await _db.Usuarios
                .Where(u => u.AzureAdObjectId == azureAdObjectId)
                .Select(u => new { u.Id, u.Nome, u.Email })
                .FirstOrDefaultAsync(ct);

            if (usuario is not null)
            {
                return new SolicitanteAtual(usuario.Id, usuario.Nome, usuario.Email);
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var usuario = await _db.Usuarios
                .Where(u => u.Email == email)
                .Select(u => new { u.Id, u.Nome, u.Email })
                .FirstOrDefaultAsync(ct);

            if (usuario is not null)
            {
                return new SolicitanteAtual(usuario.Id, usuario.Nome, usuario.Email);
            }
        }

        var autenticacaoEntraIdHabilitada = !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
        if (!autenticacaoEntraIdHabilitada)
        {
            var emailDev = email ?? "dev.local@sst";
            return new SolicitanteAtual(null, nome ?? "Usuário dev", emailDev);
        }

        return new SolicitanteAtual(null, nome, email);
    }
}

public record SolicitanteAtual(Guid? UsuarioId, string? Nome, string? Email);

public class CriarSuporteIaRequestBody
{
    public TipoSolicitacaoSuporteIa Tipo { get; set; }
    public SeveridadeSolicitacaoSuporteIa SeveridadeInformada { get; set; } = SeveridadeSolicitacaoSuporteIa.Media;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public string? UrlContexto { get; set; }
    public string? SolicitanteNome { get; set; }
    public string? SolicitanteEmail { get; set; }
}
