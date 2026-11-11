using System.Security.Claims;
using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// "Realizar integração" (decisão do usuário, 11/11/2026): o técnico conduz os admitidos de uma obra em etapas de ordem
// fixa (NR-06, NR-12, NR-18, NR-35 e OS). Nenhum endpoint anônimo. Policies do módulo Treinamento (ver / criar); além
// delas, cada handler confere se o usuário é Técnico de Segurança ou Administrador COM vínculo na obra do atendimento —
// sem acesso, 404. Contrato: docs/2026-11-11-realizar-integracao-contrato.md.
[ApiController]
[Route("api/integracoes-atendimento")]
public class IntegracoesAtendimentoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuracao;

    public IntegracoesAtendimentoController(IMediator mediator, IConfiguration configuracao)
    {
        _mediator = mediator;
        _configuracao = configuracao;
    }

    private bool AutenticacaoHabilitada => !string.IsNullOrWhiteSpace(_configuracao["AzureAd:TenantId"]);
    private string? AzureAdObjectId => User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // O que dá para fazer na obra: iniciar, continuar, quem aguarda e quem pode ser o instrutor.
    // Treinamentos › Integração: atendimentos das obras do usuário (filtros opcionais: obraId, status).
    [Authorize(Policy = "treinamento:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarIntegracoesQuery(obraId, status, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("entrada")]
    public async Task<IActionResult> Entrada([FromQuery] Guid obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterEntradaIntegracaoQuery(obraId, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost]
    public async Task<IActionResult> Iniciar(IniciarIntegracaoRequestBody body, CancellationToken ct)
    {
        Guid id;
        try
        {
            id = await _mediator.Send(new IniciarIntegracaoCommand(
            body.ObraId, body.TrabalhadoresIds ?? [], body.InstrutorTrabalhadorId, AzureAdObjectId, AutenticacaoHabilitada), ct);
        }
        catch (IntegracaoConflitoException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
        var detalhe = await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct);
        return CreatedAtAction(nameof(Obter), new { id }, detalhe);
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct);
        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/participantes")]
    public async Task<IActionResult> IncluirParticipantes(Guid id, ParticipantesIntegracaoRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new IncluirParticipantesIntegracaoCommand(id, body.TrabalhadoresIds ?? [], AzureAdObjectId, AutenticacaoHabilitada), ct);
        return Ok(await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct));
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpDelete("{id:guid}/participantes/{trabalhadorId:guid}")]
    public async Task<IActionResult> RetirarParticipante(Guid id, Guid trabalhadorId, CancellationToken ct)
    {
        await _mediator.Send(new RetirarParticipanteIntegracaoCommand(id, trabalhadorId, AzureAdObjectId, AutenticacaoHabilitada), ct);
        return Ok(await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct));
    }

    // Q9: "Não assinou" (motivo obrigatório, >= 5 caracteres); DELETE desfaz antes de avançar.
    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/etapas/{etapaId:guid}/participantes/{trabalhadorId:guid}/nao-assinou")]
    public async Task<IActionResult> MarcarNaoAssinou(Guid id, Guid etapaId, Guid trabalhadorId, NaoAssinouRequestBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new MarcarNaoAssinouEtapaIntegracaoCommand(id, etapaId, trabalhadorId, body.Motivo ?? string.Empty, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "treinamento:criar")]
    [HttpDelete("{id:guid}/etapas/{etapaId:guid}/participantes/{trabalhadorId:guid}/nao-assinou")]
    public async Task<IActionResult> DesfazerNaoAssinou(Guid id, Guid etapaId, Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new DesfazerNaoAssinouEtapaIntegracaoCommand(id, etapaId, trabalhadorId, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/etapas/{etapaId:guid}/periodos")]
    public async Task<IActionResult> RegistrarPeriodo(Guid id, Guid etapaId, PeriodoEtapaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarPeriodoEtapaIntegracaoCommand(
            id, etapaId, body.Data ?? string.Empty, body.Inicio ?? string.Empty, body.Fim ?? string.Empty, AzureAdObjectId, AutenticacaoHabilitada), ct);
        return Ok(await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct));
    }

    [Authorize(Policy = "treinamento:criar")]
    [HttpDelete("{id:guid}/etapas/{etapaId:guid}/periodos/{periodoId:guid}")]
    public async Task<IActionResult> RemoverPeriodo(Guid id, Guid etapaId, Guid periodoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoverPeriodoEtapaIntegracaoCommand(id, etapaId, periodoId, AzureAdObjectId, AutenticacaoHabilitada), ct);
        return Ok(await _mediator.Send(new ObterIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct));
    }

    // 409 com a lista do que falta (instrutor, carga, fotos, assinaturas) quando a etapa ainda não pode avançar.
    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/etapa-atual/avancar")]
    public async Task<IActionResult> Avancar(Guid id, AvancarEtapaRequestBody? body, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new AvancarEtapaIntegracaoCommand(id, body?.EtapaOrdem, AzureAdObjectId, AutenticacaoHabilitada), ct));
        }
        catch (IntegracaoBloqueadaException ex)
        {
            return Conflict(new { erro = ex.Message, faltas = ex.Faltas });
        }
    }

    [Authorize(Policy = "treinamento:ver")]
    [HttpGet("{id:guid}/ordens-servico")]
    public async Task<IActionResult> OrdensServico(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarOsIntegracaoQuery(id, AzureAdObjectId, AutenticacaoHabilitada), ct));

    [Authorize(Policy = "treinamento:criar")]
    [HttpPost("{id:guid}/concluir")]
    public async Task<IActionResult> Concluir(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new ConcluirIntegracaoCommand(id, AzureAdObjectId, AutenticacaoHabilitada), ct));
        }
        catch (IntegracaoBloqueadaException ex)
        {
            return Conflict(new { erro = ex.Message, faltas = ex.Faltas });
        }
    }

    // "Integração realizada" do perfil (Treinamentos): registro isolado, sem certificado. Escopo pelo trabalhador (404 fora da obra).
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("registros")]
    public async Task<IActionResult> Registros([FromQuery] Guid trabalhadorId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarRegistrosIntegracaoQuery(trabalhadorId), ct));

    // Relatório de leitura para o Administrador: turmas do combo ainda agendadas no modelo antigo.
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    [HttpGet("relatorio/turmas-integracao-agendadas")]
    public async Task<IActionResult> TurmasIntegracaoAgendadas(CancellationToken ct)
        => Ok(await _mediator.Send(new RelatorioTurmasIntegracaoAgendadasQuery(), ct));
}

public record IniciarIntegracaoRequestBody(Guid ObraId, List<Guid>? TrabalhadoresIds, Guid? InstrutorTrabalhadorId);
public record ParticipantesIntegracaoRequestBody(List<Guid>? TrabalhadoresIds);
public record PeriodoEtapaRequestBody(string? Data, string? Inicio, string? Fim);
public record AvancarEtapaRequestBody(int? EtapaOrdem);
public record NaoAssinouRequestBody(string? Motivo);
