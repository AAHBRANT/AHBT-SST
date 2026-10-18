using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Portal;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AAHBRANT.SST.Api.Controllers;

// Portal de terceiros (18/10/2026): a empresa terceirizada não tem conta Entra ID, então a rota é
// anônima — mesmo modelo de IdentificacaoPublicaController/ValidacaoPublicaController. O acesso é o
// link exclusivo do contrato + código por e-mail, que viram uma sessão enviada no cabeçalho
// X-Portal-Sessao em toda chamada seguinte.
//
// Com a sessão válida, o escopo por obra da requisição passa a ser SÓ a obra do contrato: os filtros
// globais do SstDbContext e os handlers reaproveitados (CadastrarPessoaTerceirizadaCommand) enxergam
// essa obra e nada mais.
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("publico")]
[Route("sst/portal-terceiros")]
public class PortalTerceirosController : ControllerBase
{
    public const string CabecalhoSessao = "X-Portal-Sessao";

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _usuarioAtual;

    public PortalTerceirosController(IMediator mediator, ICurrentUserService usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [HttpPost("acesso/codigo")]
    [EnableRateLimiting("portal-codigo")]
    public async Task<IActionResult> SolicitarCodigo([FromBody] SolicitarCodigoBody body, CancellationToken ct) =>
        Ok(await _mediator.Send(new SolicitarCodigoPortalCommand(body.Token), ct));

    [HttpPost("acesso/sessao")]
    [EnableRateLimiting("portal-codigo")]
    public async Task<IActionResult> VerificarCodigo([FromBody] VerificarCodigoBody body, CancellationToken ct) =>
        Ok(await _mediator.Send(new VerificarCodigoPortalCommand(body.Token, body.Codigo), ct));

    [HttpGet("painel")]
    public async Task<IActionResult> ObterPainel(CancellationToken ct)
    {
        var sessao = await ResolverSessaoAsync(ct);
        if (sessao is null) return Unauthorized();
        return Ok(await _mediator.Send(new ObterPainelPortalQuery(sessao.AcessoId), ct));
    }

    [HttpPost("termo")]
    public async Task<IActionResult> AceitarTermo([FromBody] AceitarTermoBody body, CancellationToken ct)
    {
        var sessao = await ResolverSessaoAsync(ct);
        if (sessao is null) return Unauthorized();
        await _mediator.Send(new AceitarTermoPortalCommand(sessao.AcessoId, body.Versao), ct);
        return NoContent();
    }

    [HttpPost("pessoas")]
    public async Task<IActionResult> CadastrarPessoa([FromBody] CadastrarPessoaPortalBody body, CancellationToken ct)
    {
        var sessao = await ResolverSessaoAsync(ct);
        if (sessao is null) return Unauthorized();
        var id = await _mediator.Send(new CadastrarPessoaPortalCommand(
            sessao.AcessoId, body.FuncaoId, body.Nome, body.Matricula, body.Cpf, body.DataAdmissao), ct);
        return Ok(new { id });
    }

    [HttpPost("pessoas/{trabalhadorId:guid}/documentos")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> EnviarDocumento(Guid trabalhadorId, [FromForm] EnviarDocumentoPortalBody body, CancellationToken ct)
    {
        var sessao = await ResolverSessaoAsync(ct);
        if (sessao is null) return Unauthorized();

        using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);
        var id = await _mediator.Send(new EnviarDocumentoPortalCommand(
            sessao.AcessoId, trabalhadorId, body.Tipo, body.Arquivo.FileName, stream.ToArray(), body.Arquivo.ContentType,
            body.DataDocumento, body.DataValidade, body.TipoExameAso, body.ResultadoAso, body.CursoTreinamentoId,
            body.CargaHoraria, body.InstituicaoInstrutor, body.NumeroCertificado), ct);
        return Ok(new { id });
    }

    private async Task<SessaoPortalResolvida?> ResolverSessaoAsync(CancellationToken ct)
    {
        var sessao = await _mediator.Send(new ResolverSessaoPortalQuery(Request.Headers[CabecalhoSessao].ToString()), ct);
        if (sessao is not null)
            _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { sessao.ObraId });
        return sessao;
    }
}

public record SolicitarCodigoBody(string Token);

public record VerificarCodigoBody(string Token, string Codigo);

public record AceitarTermoBody(string Versao);

public record CadastrarPessoaPortalBody(Guid FuncaoId, string Nome, string Matricula, string Cpf, DateTime DataAdmissao);

public class EnviarDocumentoPortalBody
{
    public IFormFile Arquivo { get; set; } = null!;
    public TipoDocumentoTerceiro Tipo { get; set; }
    public DateTime DataDocumento { get; set; }
    public DateTime DataValidade { get; set; }
    public TipoExameAso? TipoExameAso { get; set; }
    public ResultadoAso? ResultadoAso { get; set; }
    public Guid? CursoTreinamentoId { get; set; }
    public int? CargaHoraria { get; set; }
    public string? InstituicaoInstrutor { get; set; }
    public string? NumeroCertificado { get; set; }
}
