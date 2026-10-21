using AAHBRANT.SST.Application.Contratos.Commands;
using AAHBRANT.SST.Application.Contratos.Queries;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContratosController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContratosController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet]
    public async Task<IActionResult> ListarPorEmpresa([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarContratosPorEmpresaQuery(empresaId), ct));

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterDetalhe(Guid id, CancellationToken ct)
    {
        var contrato = await _mediator.Send(new ObterContratoDetalheQuery(id), ct);
        return contrato is null ? NotFound() : Ok(contrato);
    }

    // Contrato cadastrado à mão pelo técnico (18/10/2026) — enquanto a integração com o G-Juri não
    // existe, é o único caminho para abrir vagas e cadastrar pessoas terceirizadas.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarContratoCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterDetalhe), new { id }, new { id });
    }

    // Cadastro pelo PDF do contrato (18/10/2026): a IA lê empresa, contrato, vagas e pessoas e a tela
    // preenche o cadastro para o técnico conferir. Nada é gravado nesta rota.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("leitura-ia")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> LerComIa([FromForm] LerContratoComIaRequest body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Arquivo.CopyToAsync(stream, ct);
        return Ok(await _mediator.Send(new LerContratoTerceirizadoQuery(stream.ToArray()), ct));
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarContratoCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:editar")]
    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarContratoRequest request, CancellationToken ct)
    {
        var pessoasAtivas = await _mediator.Send(new EncerrarContratoCommand(id, request.DataEncerramento), ct);
        return Ok(new { pessoasAtivas });
    }

    // PDF do contrato ("Subir contrato", 21/10/2026): guardado junto do contrato; enviar de novo substitui.
    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("{id:guid}/arquivo")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> AnexarArquivo(Guid id, [FromForm] LerContratoComIaRequest body, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        await body.Arquivo.CopyToAsync(memoria, ct);
        await _mediator.Send(new AnexarArquivoContratoCommand(id, body.Arquivo.FileName, body.Arquivo.ContentType, memoria.ToArray()), ct);
        return NoContent();
    }

    [Authorize(Policy = "terceirizado:ver")]
    [HttpGet("{id:guid}/arquivo")]
    public async Task<IActionResult> ObterArquivo(Guid id, CancellationToken ct)
    {
        var arquivo = await _mediator.Send(new ObterArquivoContratoQuery(id), ct);
        return arquivo is null ? NotFound() : File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    [Authorize(Policy = "terceirizado:criar")]
    [HttpPost("{contratoId:guid}/vagas/{funcaoId:guid}/pessoas")]
    public async Task<IActionResult> CadastrarPessoaNaVaga(
        Guid contratoId, Guid funcaoId, CadastrarPessoaNaVagaRequest request, CancellationToken ct)
    {
        var trabalhadorId = await _mediator.Send(
            new CadastrarPessoaTerceirizadaCommand(contratoId, funcaoId, request.Nome, request.Matricula, request.Cpf, request.DataAdmissao),
            ct);
        return Ok(new { trabalhadorId });
    }
}

public record CadastrarPessoaNaVagaRequest(string Nome, string Matricula, string Cpf, DateTime DataAdmissao);

public record EncerrarContratoRequest(DateOnly DataEncerramento);

public class LerContratoComIaRequest
{
    public IFormFile Arquivo { get; set; } = null!;
}
