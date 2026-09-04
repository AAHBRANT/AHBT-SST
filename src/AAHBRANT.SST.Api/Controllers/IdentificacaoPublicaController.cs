using AAHBRANT.SST.Application.AreasSst.Queries;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// NTAG.md §3.B.4 — "View Contextual Pública": rota que precisa continuar acessível sem login ao
// escanear NFC/QR em campo. [AllowAnonymous] garante isso mesmo depois que a autenticação Entra ID
// for habilitada em Program.cs (hoje ainda desligada em ambiente de desenvolvimento).
[ApiController]
[AllowAnonymous]
[Route("sst/p")]
public class IdentificacaoPublicaController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdentificacaoPublicaController(IMediator mediator) => _mediator = mediator;

    // Tenta Área primeiro (aceita Código de negócio OU Uid de tag) e, se não achar, tenta Trabalhador
    // (só por Uid de tag — ver ResolverTrabalhadorPublicoQuery). O discriminador TipoRecurso em cada
    // DTO (AreaPublicaDto/TrabalhadorPublicoDto) diz ao frontend qual card renderizar.
    [HttpGet("{codigoOuUid}")]
    public async Task<IActionResult> Resolver(string codigoOuUid, CancellationToken ct)
    {
        var area = await _mediator.Send(new ResolverAreaPublicaQuery(codigoOuUid), ct);
        if (area is not null) return Ok(area);

        var trabalhador = await _mediator.Send(new ResolverTrabalhadorPublicoQuery(codigoOuUid), ct);
        return trabalhador is null ? NotFound() : Ok(trabalhador);
    }

    [HttpGet("{uid}/foto")]
    public async Task<IActionResult> ObterFotoTrabalhador(string uid, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoTrabalhadorPublicoQuery(uid), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }
}
