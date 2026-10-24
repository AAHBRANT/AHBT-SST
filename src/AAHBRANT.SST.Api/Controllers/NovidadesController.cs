using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Novidades;
using AAHBRANT.SST.Application.Novidades.Commands;
using AAHBRANT.SST.Application.Novidades.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Controllers;

// Pop-up de novidades da versão (requisito do usuário, 18/09). Policy "novidades:usar" (ver
// PermissaoAuthorizationHandler) libera qualquer usuário autenticado, sem checagem de RBAC — mesmo
// padrão de "suporte-ia:usar" — e continua funcionando em dev com Entra ID desligado. Um
// [Authorize] puro (sem Policy) quebraria em dev: cai na policy padrão RequireAuthenticatedUser, que
// falha com "No authenticationScheme was specified" quando não há AddAuthentication configurado.
[ApiController]
[Route("api/novidades")]
[Authorize(Policy = "novidades:usar")]
public class NovidadesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public NovidadesController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarNovidadesVersaoQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Criar(CriarNovidadeVersaoCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarNovidadeVersaoRequestBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new AtualizarNovidadeVersaoCommand(id, body.Titulo, body.Versao, body.DataPublicacao, body.Itens), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirNovidadeVersaoCommand(id), ct);
        return NoContent();
    }

    [HttpGet("pendente")]
    public async Task<IActionResult> ObterPendente(CancellationToken ct)
    {
        var usuarioId = await ResolverUsuarioIdAtualAsync(ct);
        var pendente = await _mediator.Send(new ObterNovidadePendenteQuery(usuarioId), ct);
        return pendente is null ? NoContent() : Ok(pendente);
    }

    [HttpPost("{id:guid}/marcar-visto")]
    public async Task<IActionResult> MarcarVisto(Guid id, CancellationToken ct)
    {
        var usuarioId = await ResolverUsuarioIdAtualAsync(ct);
        await _mediator.Send(new MarcarNovidadesComoVistasCommand(usuarioId, id), ct);
        return NoContent();
    }

    // Mesmo padrão de resolução de usuário do SuporteIaController — não há claim de negócio própria
    // pra "usuário atual" fora do middleware de escopo por obra (que só resolve TemAcessoGlobal).
    private async Task<Guid?> ResolverUsuarioIdAtualAsync(CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(azureAdObjectId))
        {
            var usuarioId = await _db.Usuarios
                .Where(u => u.AzureAdObjectId == azureAdObjectId)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);
            if (usuarioId is not null) return usuarioId;
        }

        var email = User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var usuarioId = await _db.Usuarios
                .Where(u => u.Email == email)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);
            if (usuarioId is not null) return usuarioId;
        }

        return null;
    }
}

public class AtualizarNovidadeVersaoRequestBody
{
    public string Titulo { get; set; } = string.Empty;
    public string Versao { get; set; } = string.Empty;
    public DateTime DataPublicacao { get; set; }
    public List<NovidadeVersaoItemInput> Itens { get; set; } = new();
}
