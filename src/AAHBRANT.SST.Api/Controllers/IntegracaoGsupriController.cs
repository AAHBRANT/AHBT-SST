using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.IntegracaoGsupri;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/integracoes/gsupri")]
public class IntegracaoGsupriController(IIntegracaoGsupriService service, IConfiguration config) : ControllerBase
{
    [HttpPost("v1/recebimentos")]
    [Authorize(Policy = "estoque:integracao-gsupri")]
    [RequestSizeLimit(1048576)]
    public async Task<IActionResult> Receber(RecebimentoGsupriPayload dados, CancellationToken ct)
    {
        // Fail closed mesmo quando a autenticação de desenvolvimento está desabilitada.
        // O webhook exige identidade de aplicação; permissões humanas não o habilitam.
        if (User.Identity?.IsAuthenticated != true || User.HasClaim(c => c.Type is "scp" or "http://schemas.microsoft.com/identity/claims/scope") ||
            !AppRolesReconhecidas.TemPermissao(User, "estoque:integracao-gsupri"))
            return StatusCode(403, new { erro = "Exige token de aplicação Entra ID com Sst.ReceberEstoqueGSupri." });
        if (!config.GetValue<bool>("IntegracaoGsupri:Habilitada"))
            return StatusCode(503, new { erro = "Integração G-SUPRI aguardando ativação." });
        return await Executar(async () => Ok(await service.ReceberAsync(dados, ct)));
    }

    [HttpGet("painel")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    public async Task<IActionResult> Painel(CancellationToken ct, [FromQuery] int pagina = 1)
        => Ok(await service.PainelAsync(pagina, ct));

    [HttpGet("opcoes")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    public async Task<IActionResult> Opcoes(CancellationToken ct) => Ok(await service.OpcoesAsync(ct));

    [HttpPut("vinculos/obras")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    public Task<IActionResult> VincularObra(VincularObraGsupri dados, CancellationToken ct)
        => Executar(async () => { await service.VincularObraAsync(dados, ct); return NoContent(); });

    [HttpPut("vinculos/produtos")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    public Task<IActionResult> VincularProduto(VincularProdutoGsupri dados, CancellationToken ct)
        => Executar(async () => { await service.VincularProdutoAsync(dados, ct); return NoContent(); });

    [HttpPost("recebimentos/{id:guid}/reprocessar")]
    [Authorize(Policy = PoliticasAutorizacao.SomenteAdministrador)]
    public Task<IActionResult> Reprocessar(Guid id, CancellationToken ct)
        => Executar(async () => Ok(await service.ReprocessarAsync(id, ct)));

    private async Task<IActionResult> Executar(Func<Task<IActionResult>> acao)
    {
        try { return await acao(); }
        catch (ConflitoGsupriException ex) { return Conflict(new { erro = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { erro = "Atualização simultânea. Reenvie o mesmo evento." }); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 or 1205)
        { return Conflict(new { erro = "Atualização simultânea. Reenvie o mesmo evento ou recarregue os vínculos." }); }
        catch (SqlException ex) when (ex.Number == 1205)
        { return Conflict(new { erro = "Atualização simultânea. Reenvie o mesmo evento." }); }
    }
}
