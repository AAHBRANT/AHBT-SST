using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Câmeras de monitoramento de EPI (capacete e colete). Módulo em PILOTO (09/11/2026): toda action exige
// PoliticasAutorizacao.CamerasPiloto (Administrador nomeado em Cameras:UsuariosPiloto). A policy fica em cada
// action, e não na classe, porque [Authorize] é cumulativo e /acesso precisa ser alcançável por qualquer
// usuário autenticado (o teste CamerasPilotoPoliticaTests garante que nenhuma action fica sem a policy).
[ApiController]
[Route("api/[controller]")]
public class CamerasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuthorizationService _autorizacao;

    public CamerasController(IMediator mediator, IWebHostEnvironment environment, IAuthorizationService autorizacao)
    {
        _mediator = mediator;
        _environment = environment;
        _autorizacao = autorizacao;
    }

    // Só para o menu decidir se mostra o item sem receber 403: qualquer autenticado consulta, e a resposta vem
    // da própria policy do piloto (mesma regra, sem duplicar).
    [Authorize(Policy = PoliticasAutorizacao.QualquerUsuarioAutenticado)]
    [HttpGet("acesso")]
    public async Task<IActionResult> Acesso()
    {
        var resultado = await _autorizacao.AuthorizeAsync(User, PoliticasAutorizacao.CamerasPiloto);
        return Ok(new { permitido = resultado.Succeeded });
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCamerasQuery(obraId), ct));

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarCameraCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Listar), new { obraId = command.ObraId }, new { id });
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarCameraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("O id da rota difere do id do corpo.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirCameraCommand(id), ct);
        return NoContent();
    }

    // Recebe um quadro JPEG da tela Monitorar. Sempre responde 200 com a situação
    // (ignorado/limpo/ocorrencia/falha) para o navegador seguir monitorando sem tratar erro.
    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost("{id:guid}/quadros")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnalisarQuadro(Guid id, [FromForm] QuadroCameraForm form, CancellationToken ct)
    {
        if (form.Foto is null || form.Foto.Length == 0) return BadRequest("Envie a foto do quadro.");

        using var memoria = new MemoryStream();
        await form.Foto.CopyToAsync(memoria, ct);
        var capturadaEmUtc = (form.CapturadaEm ?? DateTimeOffset.UtcNow).UtcDateTime;

        return Ok(await _mediator.Send(new AnalisarQuadroCameraCommand(id, memoria.ToArray(), capturadaEmUtc), ct));
    }

    // Gestão dos agentes instalados nas obras. Policy do piloto = só Administrador nomeado (mesma regra de excluir câmera).
    // O segredo sai UMA vez, na resposta do cadastro; no banco fica só o hash.
    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost("agentes")]
    public async Task<IActionResult> CriarAgente(CriarAgenteCameraRequest corpo, CancellationToken ct)
    {
        var registro = await _mediator.Send(new CriarAgenteCameraCommand(corpo.ObraId, corpo.Nome ?? string.Empty), ct);
        Response.Headers.CacheControl = "no-store";
        return CreatedAtAction(nameof(ListarAgentes), new { obraId = corpo.ObraId }, new { id = registro.Id, segredo = registro.Segredo });
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet("agentes")]
    public async Task<IActionResult> ListarAgentes([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarAgentesCameraQuery(obraId), ct));

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpDelete("agentes/{id:guid}")]
    public async Task<IActionResult> RevogarAgente(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RevogarAgenteCameraCommand(id), ct);
        return NoContent();
    }

    // Equipamentos (câmeras IP / gravadores da rede local da obra). A senha só entra por aqui (criar ou trocar) e NUNCA volta:
    // a lista devolve apenas "senhaDefinida". Excluir é exclusão lógica e a policy do piloto já é só Administrador nomeado.
    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet("equipamentos")]
    public async Task<IActionResult> ListarEquipamentos([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarEquipamentosCameraQuery(obraId), ct));

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost("equipamentos")]
    public async Task<IActionResult> CriarEquipamento(CriarEquipamentoCameraCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ListarEquipamentos), new { obraId = command.ObraId }, new { id });
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPut("equipamentos/{id:guid}")]
    public async Task<IActionResult> AtualizarEquipamento(Guid id, AtualizarEquipamentoCameraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("O id da rota difere do id do corpo.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPut("equipamentos/{id:guid}/senha")]
    public async Task<IActionResult> DefinirSenhaEquipamento(Guid id, DefinirSenhaEquipamentoRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new DefinirSenhaEquipamentoCameraCommand(id, corpo.Senha ?? string.Empty), ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpDelete("equipamentos/{id:guid}")]
    public async Task<IActionResult> ExcluirEquipamento(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirEquipamentoCameraCommand(id), ct);
        return NoContent();
    }

    // "Testar conexão": o servidor não alcança a câmera, então estas rotas só criam o PEDIDO (202 com o id do teste); o agente da
    // obra o executa e o front consulta o resultado em GET testes-conexao/{id}.
    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost("{id:guid}/teste-conexao")]
    public async Task<IActionResult> TestarConexaoDaCamera(Guid id, CancellationToken ct)
        => Accepted(new { id = await _mediator.Send(new SolicitarTesteConexaoCameraCommand(CameraId: id), ct) });

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPost("equipamentos/{id:guid}/teste-conexao")]
    public async Task<IActionResult> TestarConexaoDoEquipamento(Guid id, TesteConexaoEquipamentoRequest? corpo, CancellationToken ct)
        => Accepted(new { id = await _mediator.Send(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: id, Canal: corpo?.Canal, Stream: corpo?.Stream), ct) });

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet("testes-conexao/{id:guid}")]
    public async Task<IActionResult> ObterTesteConexao(Guid id, CancellationToken ct)
    {
        var teste = await _mediator.Send(new ObterTesteConexaoCameraQuery(id), ct);
        // Pode trazer a miniatura (imagem de pessoa possível): sem cache de navegador ou proxy.
        Response.Headers.CacheControl = "private, no-store";
        return Ok(teste);
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet("ocorrencias")]
    public async Task<IActionResult> ListarOcorrencias(
        [FromQuery] Guid? obraId,
        [FromQuery] Guid? cameraId,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] SituacaoOcorrenciaEpi? situacao,
        CancellationToken ct)
        => Ok(await _mediator.Send(new ListarOcorrenciasEpiQuery(obraId, cameraId, de?.UtcDateTime, ate?.UtcDateTime, situacao), ct));

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpGet("ocorrencias/{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoOcorrenciaEpiQuery(id), ct);
        // Foto de pessoa (LGPD): não pode ficar em cache de navegador ou proxy.
        Response.Headers.CacheControl = "private, no-store";
        return File(foto, "image/jpeg");
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPut("ocorrencias/{id:guid}/trabalhador")]
    public async Task<IActionResult> VincularTrabalhador(Guid id, VincularTrabalhadorRequest corpo, CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(azureAdObjectId) && _environment.IsDevelopment())
            azureAdObjectId = "dev-local-user";

        await _mediator.Send(new VincularTrabalhadorOcorrenciaEpiCommand(id, corpo.TrabalhadorId, azureAdObjectId), ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.CamerasPiloto)]
    [HttpPut("ocorrencias/{id:guid}/situacao")]
    public async Task<IActionResult> DefinirSituacao(Guid id, DefinirSituacaoRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new DefinirSituacaoOcorrenciaEpiCommand(id, corpo.Situacao), ct);
        return NoContent();
    }
}

public class QuadroCameraForm
{
    public IFormFile? Foto { get; set; }
    public DateTimeOffset? CapturadaEm { get; set; }
}

public record CriarAgenteCameraRequest(Guid ObraId, string? Nome);

public record DefinirSenhaEquipamentoRequest(string? Senha)
{
    public override string ToString() => nameof(DefinirSenhaEquipamentoRequest);
}

public record TesteConexaoEquipamentoRequest(int? Canal, int? Stream);

public record VincularTrabalhadorRequest(Guid? TrabalhadorId);

public record DefinirSituacaoRequest(SituacaoOcorrenciaEpi Situacao);
