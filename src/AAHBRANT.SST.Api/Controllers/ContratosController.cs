using AAHBRANT.SST.Application.Contratos.Commands;
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

    // Contrato cadastrado à mão pelo técnico (18/10/2026) — enquanto a integração com o G-Juri não
    // existe, é o único caminho para abrir vagas e cadastrar pessoas terceirizadas.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarContratoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarContratoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarContratoRequest request, CancellationToken ct)
    {
        var pessoasAtivas = await _mediator.Send(new EncerrarContratoCommand(id, request.DataEncerramento), ct);
        return Ok(new { pessoasAtivas });
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

public record EncerrarContratoRequest(DateOnly DataEncerramento);
