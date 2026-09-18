using AAHBRANT.SST.Application.Contratos.Queries;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContratosController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContratosController(IMediator mediator) => _mediator = mediator;

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
}

public record CadastrarPessoaNaVagaRequest(string Nome, string Matricula, string Cpf, DateTime DataAdmissao);
