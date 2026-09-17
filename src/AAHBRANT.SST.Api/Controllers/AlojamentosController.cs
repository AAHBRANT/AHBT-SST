using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Alojamentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlojamentosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public AlojamentosController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [Authorize(Policy = "alojamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
    {
        var resultado = await _mediator.Send(new ListarAlojamentosQuery(obraId), ct);
        return Ok(resultado);
    }

    [Authorize(Policy = "alojamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarAlojamentoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }

    // Carga inicial única do cadastro do G-RH (Integração G-RH). A atualização contínua depois disso é
    // automática, via evento do Service Bus (ServiceBusAlojamentoGrhProcessor) — mesmo padrão de
    // TrabalhadoresController.ImportarDoGrh.
    [Authorize(Policy = "alojamento:criar")]
    [HttpPost("importar-grh")]
    public async Task<IActionResult> ImportarDoGrh(CancellationToken ct)
        => Ok(await _mediator.Send(new ImportarAlojamentosGrhCommand(), ct));

    [Authorize(Policy = "alojamento:gerenciar-moradores")]
    [HttpPost("{alojamentoId:guid}/moradores")]
    public async Task<IActionResult> AdicionarMorador(Guid alojamentoId, [FromBody] Guid trabalhadorId, CancellationToken ct)
    {
        var id = await _mediator.Send(new AdicionarMoradorAlojamentoCommand(alojamentoId, trabalhadorId), ct);
        return CreatedAtAction(nameof(Listar), null, new { id });
    }

    [Authorize(Policy = "alojamento:gerenciar-moradores")]
    [HttpDelete("moradores/{alojamentoMoradorId:guid}")]
    public async Task<IActionResult> RemoverMorador(Guid alojamentoMoradorId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverMoradorAlojamentoCommand(alojamentoMoradorId), ct);
        return NoContent();
    }

    [Authorize(Policy = "alojamento:ver")]
    [HttpGet("configuracao")]
    public async Task<IActionResult> ObterConfiguracao(CancellationToken ct) =>
        Ok(new { diasParaInspecaoAtrasada = await _mediator.Send(new ObterConfiguracaoAlojamentoQuery(), ct) });

    [Authorize(Policy = "alojamento:configurar")]
    [HttpPut("configuracao")]
    public async Task<IActionResult> AtualizarConfiguracao(AtualizarConfiguracaoAlojamentoCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // Endpoint atômico "obter ou criar" (Task 5): abrir a aba Inspeções de um alojamento sempre
    // resolve para a inspeção em andamento (retoma se já existe, cria se não existe) — o usuário
    // logado é sempre o responsável, extraído do ClaimsPrincipal (claim "oid"), mesmo padrão de
    // DdsSemanalController/CalendarioController/AssinaturaController/SessoesTreinamentoController.
    [Authorize(Policy = "inspecao:criar")]
    [HttpPost("{alojamentoId:guid}/inspecao-atual")]
    public async Task<IActionResult> ObterOuCriarInspecaoAtual(Guid alojamentoId, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        var resultado = await _mediator.Send(new ObterOuCriarInspecaoAlojamentoCommand(alojamentoId, azureAdObjectId), ct);
        return Ok(resultado);
    }
}
