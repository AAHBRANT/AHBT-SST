using AAHBRANT.SST.Application.Obras.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

/// <summary>
/// Canal unidirecional SST -> app de Comunicados AAHBRANT (24/11/2026): o app de comunicados monta os
/// "Comunicados de obra" (cards por obra, arte com a logo da obra) usando o cadastro de obras do SST
/// como fonte única — obra nova cadastrada aqui aparece lá sozinha, com a logo.
///
/// Só leitura e só de obras (nome, código, cliente, status e logo). Usa uma permissão DEDICADA
/// ("obra:integracao-comunicados"), não "organizacional:ver": aquela abriria também equipes,
/// funções, setores e dispositivos, que o app de comunicados não precisa. O token client-credentials
/// do app de comunicados carrega a App Role <c>Sst.LerObrasComunicados</c>, reconhecida em
/// <see cref="Api.Autorizacao.AppRolesReconhecidas"/> (mesmo mecanismo de G-RH, G-Juri e G-Supri).
/// Nenhum usuário humano tem essa permissão.
/// </summary>
[ApiController]
[Route("api/integracoes/comunicados")]
public class IntegracaoComunicadosController : ControllerBase
{
    private readonly IMediator _mediator;

    public IntegracaoComunicadosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "obra:integracao-comunicados")]
    [HttpGet("obras")]
    public async Task<IActionResult> ListarObras(CancellationToken ct)
    {
        var obras = await _mediator.Send(new ListarObrasQuery(), ct);
        // Só o necessário para o card e a arte — sem endereço, CNPJ, CNAE etc.
        return Ok(obras.Select(o => new
        {
            o.Id,
            o.Codigo,
            o.Nome,
            o.Cliente,
            Status = o.Status.ToString(),
            o.TemLogo
        }));
    }

    [Authorize(Policy = "obra:integracao-comunicados")]
    [HttpGet("obras/{id:guid}/logo")]
    public async Task<IActionResult> ObterLogo(Guid id, CancellationToken ct)
    {
        var logo = await _mediator.Send(new ObterLogoObraQuery(id), ct);
        return logo is null ? NotFound() : File(logo.Conteudo, logo.ContentType);
    }
}
