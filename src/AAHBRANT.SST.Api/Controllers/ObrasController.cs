using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Obras.Commands;
using AAHBRANT.SST.Application.Obras.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ObrasController : ControllerBase
{
    private readonly IMediator _mediator;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObrasController> _logger;

    public ObrasController(IMediator mediator, IServiceScopeFactory scopeFactory, ILogger<ObrasController> logger)
    {
        _mediator = mediator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _mediator.Send(new ListarObrasQuery(), ct));

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var obra = await _mediator.Send(new ObterObraPorIdQuery(id), ct);
        return obra is null ? NotFound() : Ok(obra);
    }

    // Logomarca obrigatória no cadastro (decisão do usuário, 31/08) — o corpo passa a ser
    // multipart/form-data (como o endpoint de anexar logo) em vez de JSON, para poder exigir o
    // arquivo já na criação da obra em vez de um segundo passo opcional.
    [Authorize(Policy = "organizacional:criar")]
    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Criar([FromForm] CriarObraRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Logo.CopyToAsync(stream, ct);

        var command = new CriarObraCommand(
            body.Codigo,
            body.Nome,
            body.Cliente,
            body.Status,
            body.DataInicio,
            body.DataPrevisaoTermino,
            body.Endereco,
            body.Cidade,
            body.Uf,
            body.Cnpj,
            stream.ToArray(),
            body.Logo.ContentType);

        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarObraCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    public record AtualizarMetodosAssinaturaRequestBody(bool Biometria, bool ReconhecimentoFacial);

    [Authorize(Policy = "organizacional:editar")]
    [HttpPut("{id:guid}/metodos-assinatura")]
    public async Task<IActionResult> AtualizarMetodosAssinatura(Guid id, AtualizarMetodosAssinaturaRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new AtualizarMetodosAssinaturaObraCommand(id, body.Biometria, body.ReconhecimentoFacial), ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirObraCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "organizacional:editar")]
    [HttpPost("{id:guid}/logo")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarLogo(Guid id, [FromForm] AnexarLogoObraRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Logo.CopyToAsync(stream, ct);

        var command = new AnexarLogoObraCommand(id, stream.ToArray(), body.Logo.ContentType);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    // Reconstrói o grupo facial da obra no Azure a partir das fotos de cadastro já guardadas (ninguém é
    // fotografado de novo). Para quando o banco aponta para um grupo que não existe mais no Azure em uso
    // e todo rosto da obra volta como "não reconhecido". Reservado ao Administrador (mesma policy da
    // exclusão). Leva minutos (uma chamada ao Azure por pessoa, respeitando a cota), por isso responde 202
    // e roda em segundo plano; o resultado vai para o log da API.
    [Authorize(Policy = "organizacional:excluir")]
    [HttpPost("{id:guid}/facial/recriar-grupo")]
    public IActionResult RecriarGrupoFacial(Guid id)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var escopo = _scopeFactory.CreateScope();
                var facial = escopo.ServiceProvider.GetRequiredService<IAutenticacaoFacialService>();
                var resultado = await facial.MigrarModeloReconhecimentoAsync(id, CancellationToken.None);
                if (resultado is null)
                    _logger.LogInformation("Obra {ObraId}: grupo facial já está íntegro, nada a recriar.", id);
                else
                    _logger.LogInformation("Obra {ObraId}: grupo facial recriado com {Migrados} cadastro(s); refazer: {Refazer}.",
                        id, resultado.Migrados, resultado.SemCadastroAposMigracao.Count == 0 ? "ninguém" : string.Join(", ", resultado.SemCadastroAposMigracao));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao recriar o grupo facial da obra {ObraId}.", id);
            }
        });
        return Accepted();
    }

    // Confere os cadastros faciais da obra contra o grupo no Azure (só leitura): funcionários com vínculo
    // quebrado (refazer o cadastro facial), pessoas soltas no Azure e vínculos duplicados no banco. Mesma
    // policy de recriar o grupo: é diagnóstico do Administrador.
    [Authorize(Policy = "organizacional:excluir")]
    [HttpGet("{id:guid}/facial/diagnostico-vinculos")]
    public async Task<IActionResult> DiagnosticarVinculosFaciais(Guid id, [FromServices] IAutenticacaoFacialService facial, CancellationToken ct)
        => Ok(await facial.DiagnosticarVinculosAsync(id, ct));

    [Authorize(Policy = "organizacional:ver")]
    [HttpGet("{id:guid}/logo")]
    public async Task<IActionResult> ObterLogo(Guid id, CancellationToken ct)
    {
        var logo = await _mediator.Send(new ObterLogoObraQuery(id), ct);
        return logo is null ? NotFound() : File(logo.Conteudo, logo.ContentType, logo.NomeArquivo);
    }
}

public class AnexarLogoObraRequestBody
{
    public IFormFile Logo { get; set; } = null!;
}

public class CriarObraRequestBody
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public StatusObra Status { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataPrevisaoTermino { get; set; }
    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cnpj { get; set; }
    public IFormFile Logo { get; set; } = null!;
}
