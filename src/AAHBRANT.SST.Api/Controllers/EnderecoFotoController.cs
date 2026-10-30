using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Endereço por extenso para o carimbo da foto (rua, bairro, cidade/UF, CEP, país), no padrão do G-QUALI. Sempre 200:
// sem serviço ou sem resposta devolve lista vazia e a foto sai só com as coordenadas.
[ApiController]
[Authorize]
public class EnderecoFotoController(IGeocodificadorReverso geocodificador) : ControllerBase
{
    [HttpGet("api/fotos/endereco")]
    public async Task<IActionResult> Endereco([FromQuery] double latitude, [FromQuery] double longitude, CancellationToken ct)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["posicao"] = ["Latitude ou longitude inválida."] }));
        return Ok(new { linhas = await geocodificador.LinhasAsync(latitude, longitude, ct) });
    }
}
