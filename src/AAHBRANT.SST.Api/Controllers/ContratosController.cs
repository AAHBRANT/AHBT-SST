using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Contratos.Queries;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Application.Terceirizados.Portal;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContratosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public ContratosController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet]
    public async Task<IActionResult> ListarPorEmpresa([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarContratosPorEmpresaQuery(empresaId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var contrato = await _mediator.Send(new ObterContratoDetalheQuery(id), ct);
        return contrato is null ? NotFound() : Ok(contrato);
    }

    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("{contratoId:guid}/vagas/{funcaoId:guid}/pessoas")]
    public async Task<IActionResult> CadastrarPessoaNaVaga(
        Guid contratoId, Guid funcaoId, CadastrarPessoaNaVagaRequest request, CancellationToken ct)
    {
        var trabalhadorId = await _mediator.Send(
            new CadastrarPessoaTerceirizadaCommand(contratoId, funcaoId, request.Nome, request.Matricula, request.Cpf, request.DataAdmissao),
            ct);
        return Ok(new { trabalhadorId });
    }

    // Portal de terceiros: gera o link do contrato e envia por e-mail à empresa. Gerar de novo revoga
    // o link anterior. O link volta na resposta para o técnico repassar se o e-mail falhar.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("{contratoId:guid}/acesso-portal")]
    public async Task<IActionResult> GerarAcessoPortal(Guid contratoId, GerarAcessoPortalRequest? request, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        return Ok(await _mediator.Send(new GerarAcessoPortalTerceiroCommand(contratoId, request?.Email, usuarioId), ct));
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{contratoId:guid}/acesso-portal")]
    public async Task<IActionResult> ListarAcessosPortal(Guid contratoId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAcessosPortalTerceiroQuery(contratoId), ct));

    [Authorize(Policy = "terceirizado:editar")]
    [HttpDelete("acesso-portal/{acessoId:guid}")]
    public async Task<IActionResult> RevogarAcessoPortal(Guid acessoId, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new RevogarAcessoPortalTerceiroCommand(acessoId, usuarioId), ct);
        return NoContent();
    }
}

public record CadastrarPessoaNaVagaRequest(string Nome, string Matricula, string Cpf, DateTime DataAdmissao);

public record GerarAcessoPortalRequest(string? Email);
