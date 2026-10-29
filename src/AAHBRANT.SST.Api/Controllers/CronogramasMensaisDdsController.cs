using System.Security.Claims;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Cronogramas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/cronogramasmensaisdds")]
public class CronogramasMensaisDdsController(IMediator mediator, IAppDbContext db,
    IEmissorCronogramaMensalDds emissor, IWebHostEnvironment environment) : ControllerBase
{
    [Authorize(Policy = "dds:ver")]
    [HttpGet("{obraId:guid}/{ano:int}/{mes:int}")]
    public async Task<IActionResult> Obter(Guid obraId, int ano, int mes, CancellationToken ct) =>
        Ok(await mediator.Send(new ObterCronogramaMensalDdsQuery(obraId, ano, mes), ct));

    [Authorize(Policy = "dds:criar")]
    [HttpPut("{obraId:guid}/{ano:int}/{mes:int}")]
    public async Task<IActionResult> Salvar(Guid obraId, int ano, int mes, SalvarCronogramaDdsBody body, CancellationToken ct)
    {
        try
        {
            var oid = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(oid) && environment.IsDevelopment()) oid = "dev-local-user";
            return Ok(await mediator.Send(new SalvarCronogramaMensalDdsCommand(obraId, ano, mes, body.Versao, body.Dias, oid), ct));
        }
        catch (DbUpdateConcurrencyException) { return Conflito(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { return Conflito(); }
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpPost("{id:guid}/pdf")]
    public async Task<IActionResult> Gerar(Guid id, GerarCronogramaDdsPdfBody body, CancellationToken ct)
    {
        try
        {
            var arquivo = await emissor.GerarAsync(id, body.Versao, body.SemanaCompleta, body.Emitir, ct);
            return File(arquivo.Conteudo, "application/pdf", arquivo.NomeArquivo);
        }
        catch (DbUpdateConcurrencyException) { return Conflito(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 or 1205 }) { return Conflito(); }
    }

    [Authorize(Policy = "dds:exportar")]
    [HttpGet("{id:guid}/revisoes/{revisaoId:guid}/pdf")]
    public async Task<IActionResult> BaixarRevisao(Guid id, Guid revisaoId, CancellationToken ct)
    {
        var revisao = await db.CronogramasMensaisDdsRevisoes.AsNoTracking()
            .Where(r => r.Id == revisaoId && r.CronogramaId == id)
            .Select(r => new { r.Numero, Pdf = r.DocumentoAssinatura!.PdfConteudo }).FirstOrDefaultAsync(ct);
        return revisao?.Pdf is null ? NotFound() : File(revisao.Pdf, "application/pdf", $"cronograma-dds-r{revisao.Numero:00}.pdf");
    }

    private ConflictObjectResult Conflito() =>
        Conflict(new { erro = "Este cronograma foi alterado por outra pessoa. Suas alterações continuam na tela; recarregue o mês para conferir antes de tentar novamente." });
}

public record SalvarCronogramaDdsBody(int Versao, List<DiaCronogramaDdsEntrada> Dias);
public record GerarCronogramaDdsPdfBody(int Versao, bool SemanaCompleta, bool Emitir);
