using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Página "Relatórios": histórico dos relatórios gerados (lista de presença diária, boletim semanal etc.). A lista
// de presença é dado do DDS, então a leitura usa a mesma permissão do DDS (dds:ver). O escopo por obra vem dos
// filtros globais: quem não tem acesso à obra não vê o relatório.
[ApiController]
[Route("api/[controller]")]
public class RelatoriosController : ControllerBase
{
    private readonly IMediator _mediator;

    public RelatoriosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "dds:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] TipoRelatorio? tipo, [FromQuery] Guid? obraId, [FromQuery] int limite = 60, CancellationToken ct = default)
        => Ok(await _mediator.Send(new ListarRelatoriosQuery(tipo, obraId, limite), ct));

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}/imagem")]
    public async Task<IActionResult> Imagem(Guid id, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterImagemRelatorioQuery(id), ct);
        return arquivo is null ? NotFound() : File(arquivo.Conteudo, arquivo.ContentType);
    }

    [Authorize(Policy = "dds:ver")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterPdfRelatorioQuery(id), ct);
        return arquivo is null ? NotFound() : File(arquivo.Conteudo, arquivo.ContentType, arquivo.Nome);
    }
}

// Tela "Destinatários dos relatórios" (Administração). Só Administrador: é regra fixa, igual à exclusão de
// registros (a matriz de permissões é preenchida à mão, então um código novo ficaria trancado para todos).
[ApiController]
[Route("api/destinatarios-relatorio")]
[Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
public class DestinatariosRelatorioController : ControllerBase
{
    private readonly IMediator _mediator;

    public DestinatariosRelatorioController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarDestinatariosRelatorioQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Salvar(SalvarDestinatarioRelatorioCommand command, CancellationToken ct)
        => Ok(new { id = await _mediator.Send(command, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RemoverDestinatarioRelatorioCommand(id), ct);
        return NoContent();
    }

    // Pré-preenche pelos perfis (Técnico e Engenheiro de Segurança de cada obra). Não mexe em quem já está na lista.
    [HttpPost("sugerir-por-perfil")]
    public async Task<IActionResult> SugerirPorPerfil(CancellationToken ct)
        => Ok(new { criados = await _mediator.Send(new SugerirDestinatariosPorPerfilCommand(), ct) });
}
