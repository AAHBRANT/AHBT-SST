using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.GeracaoPgr;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Assistente "Gerar PGR pela EAP" (21/10/2026). Quem elabora precisa de pgr:criar; quem aprova é o
// Engenheiro de Segurança (regra fixa por perfil). Quem elabora/aprova vem sempre do token.
[ApiController]
[Route("api/geracoes-pgr-eap")]
public class GeracoesPgrEapController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public GeracoesPgrEapController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    public record SalvarRequest(int Passo, EstruturaGeracaoPgr Estrutura);

    public record GerarRiscosRequest(int GheNumero, string CodigoEap);

    public record DevolverRequest(string Motivo);

    [Authorize(Policy = "pgr:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarGeracoesPgrEapQuery(obraId), ct));

    [Authorize(Policy = "pgr:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterGeracaoPgrEapQuery(id), ct));

    [Authorize(Policy = "pgr:criar")]
    [HttpPost]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarGeracaoRequest corpo, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await corpo.Arquivo.CopyToAsync(stream, ct);
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var id = await _mediator.Send(new CriarGeracaoPgrEapCommand(
            corpo.ObraId, corpo.Nome, corpo.DataElaboracao, corpo.DataProximaRevisao,
            corpo.Arquivo.FileName, corpo.Arquivo.ContentType, stream.ToArray(), usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "pgr:criar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Salvar(Guid id, SalvarRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new SalvarGeracaoPgrEapCommand(id, corpo.Passo, corpo.Estrutura), ct);
        return NoContent();
    }

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("{id:guid}/sugerir-ghes")]
    public async Task<IActionResult> SugerirGhes(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SugerirGhesGeracaoPgrCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("{id:guid}/gerar-riscos")]
    public async Task<IActionResult> GerarRiscos(Guid id, GerarRiscosRequest corpo, CancellationToken ct)
        => Ok(new { riscos = await _mediator.Send(new GerarRiscosAtividadeGeracaoPgrCommand(id, corpo.GheNumero, corpo.CodigoEap), ct) });

    [Authorize(Policy = "pgr:criar")]
    [HttpPost("{id:guid}/enviar-aprovacao")]
    public async Task<IActionResult> EnviarAprovacao(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EnviarGeracaoPgrParaAprovacaoCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("{id:guid}/devolver")]
    public async Task<IActionResult> Devolver(Guid id, DevolverRequest corpo, CancellationToken ct)
    {
        await _mediator.Send(new DevolverGeracaoPgrCommand(id, corpo.Motivo), ct);
        return NoContent();
    }

    // Sugestão do plano de emergência: locais, rotas e recomendação da IA a partir do endereço da obra.
    // Não grava nada; o técnico aplica o que quiser na ficha e salva.
    [Authorize(Policy = "pgr:criar")]
    [HttpPost("{id:guid}/sugerir-emergencia")]
    public async Task<IActionResult> SugerirEmergencia(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new SugerirEmergenciaGeracaoPgrCommand(id), ct));

    // PDF do PGR aprovado (só depois da aprovação do Engenheiro de Segurança).
    [Authorize(Policy = "pgr:ver")]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> BaixarPdf(Guid id, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new GerarPdfGeracaoPgrEapQuery(id), ct);
        return File(arquivo.Conteudo, "application/pdf", arquivo.NomeArquivo);
    }

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var pgrId = await _mediator.Send(new AprovarGeracaoPgrCommand(id, usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { pgrId });
    }
}

public class CriarGeracaoRequest
{
    public Guid ObraId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataElaboracao { get; set; }
    public DateTime? DataProximaRevisao { get; set; }
    public IFormFile Arquivo { get; set; } = null!;
}
