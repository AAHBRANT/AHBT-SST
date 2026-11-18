using AAHBRANT.SST.Application.AreasSst.Queries;
using AAHBRANT.SST.Application.Inspecoes.Adesivo;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// NTAG.md §3.B.4 — "View Contextual Pública": precisa abrir sem login, porque quem encosta o
// celular na tag do capacete em campo (fiscal, encarregado, auditor) não está dentro do Teams e o
// app só consegue token via Teams SSO (ver TeamsApp/src/lib/authHeaders.ts). Exigir [Authorize] aqui
// derrubava toda leitura de NFC para 401 — que o frontend ainda mostra como "tag não encontrada".
// O controle de acesso é a posse física da tag: o UID é opaco e não enumerável (mesmo modelo de
// ValidacaoPublicaController). Dado sensível de saúde não sai nesta rota sem login — ver
// ResolverTrabalhadorPublicoQuery.
[ApiController]
[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("publico")]
[Route("sst/p")]
public class IdentificacaoPublicaController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdentificacaoPublicaController(IMediator mediator) => _mediator = mediator;

    // Tenta Área primeiro (aceita Código de negócio OU Uid de tag) e, se não achar, tenta Trabalhador
    // (só por Uid de tag — ver ResolverTrabalhadorPublicoQuery) e por fim Equipamento (adesivo de inspeção). O discriminador TipoRecurso em cada
    // DTO (AreaPublicaDto/TrabalhadorPublicoDto) diz ao frontend qual card renderizar.
    [HttpGet("{codigoOuUid}")]
    public async Task<IActionResult> Resolver(string codigoOuUid, [FromQuery(Name = "i")] string? protocoloAdesivo, CancellationToken ct)
    {
        var area = await _mediator.Send(new ResolverAreaPublicaQuery(codigoOuUid), ct);
        if (area is not null) return Ok(area);

        var autenticado = User.Identity?.IsAuthenticated == true;
        var trabalhador = await _mediator.Send(new ResolverTrabalhadorPublicoQuery(codigoOuUid, autenticado), ct);
        if (trabalhador is not null) return Ok(trabalhador);

        // Adesivo de inspeção (veículo, item de tema ou alojamento): situação ATUAL do objeto. O "?i=" do QR é o
        // protocolo da inspeção que gerou o adesivo, usado só para a tela avisar que o adesivo ficou antigo.
        var equipamento = await _mediator.Send(new ResolverEquipamentoPublicoQuery(codigoOuUid, protocoloAdesivo), ct);
        return equipamento is null ? NotFound() : Ok(equipamento);
    }

    [HttpGet("{uid}/foto")]
    public async Task<IActionResult> ObterFotoTrabalhador(string uid, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoTrabalhadorPublicoQuery(uid), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }
}
