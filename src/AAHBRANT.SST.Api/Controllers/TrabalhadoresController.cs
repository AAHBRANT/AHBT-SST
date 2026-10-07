using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrabalhadoresController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public TrabalhadoresController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTrabalhadoresQuery(obraId), ct));

    // Indicador "Podem trabalhar hoje" do Início: quantos ativos estão liberados e por que os demais não.
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("liberacao")]
    public async Task<IActionResult> ObterLiberacao([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterLiberacaoParaTrabalhoQuery(obraId), ct));

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var trabalhador = await _mediator.Send(new ObterTrabalhadorPorIdQuery(id), ct);
        return trabalhador is null ? NotFound() : Ok(trabalhador);
    }

    // Perfil de Vida do Trabalhador — agrega ASO/EPI/Treinamentos/Riscos/Ocorrências/Assinaturas numa
    // única chamada (ver ObterPerfilCompletoTrabalhadorQuery).
    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/perfil-completo")]
    public async Task<IActionResult> ObterPerfilCompleto(Guid id, CancellationToken ct)
    {
        var perfil = await _mediator.Send(new ObterPerfilCompletoTrabalhadorQuery(id), ct);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/relatorio-pdf")]
    public async Task<IActionResult> ObterRelatorioFiscalizacao(Guid id, CancellationToken ct)
    {
        var pdf = await _mediator.Send(new GerarRelatorioFiscalizacaoTrabalhadorQuery(id), ct);
        if (pdf is null) return NotFound();
        return File(pdf, "application/pdf", $"relatorio-fiscalizacao-{id}.pdf");
    }

    [Authorize(Policy = "trabalhador:criar")]
    [HttpPost]
    public async Task<IActionResult> Criar(CriarTrabalhadorCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    // Carga inicial única do cadastro vindo do G-RH (Integração G-RH) — chamado manualmente uma vez
    // pela tela de Administração depois que "Grh:ClientSecret" estiver configurado. A atualização
    // contínua depois disso é automática, via evento do Service Bus.
    [Authorize(Policy = "trabalhador:criar")]
    [HttpPost("importar-grh")]
    public async Task<IActionResult> ImportarDoGrh(CancellationToken ct)
        => Ok(await _mediator.Send(new ImportarColaboradoresGrhCommand(), ct));

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarTrabalhadorCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Id da rota difere do corpo da requisição.");
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:excluir")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ExcluirTrabalhadorCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPost("{id:guid}/foto")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> AnexarFoto(Guid id, [FromForm] AnexarFotoTrabalhadorRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);

        var command = new AnexarFotoTrabalhadorCommand(id, stream.ToArray(), body.Foto.ContentType);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/foto")]
    public async Task<IActionResult> ObterFoto(Guid id, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoTrabalhadorQuery(id), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/termo-aceite")]
    public async Task<IActionResult> RegistrarTermoAceiteAssinatura(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarTermoAceiteAssinaturaCommand(id), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/consentimento-biometria")]
    public async Task<IActionResult> RegistrarConsentimentoBiometria(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RegistrarConsentimentoBiometriaCommand(id), ct);
        return NoContent();
    }

    // Funcionários com 3 ou mais falhas de reconhecimento facial em 30 dias contra o cadastro atual:
    // candidatos a refazer a foto. O escopo por obra vem dos filtros globais.
    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpGet("cadastros-faciais-fracos")]
    public async Task<IActionResult> ListarCadastrosFaciaisFracos([FromQuery] Guid? obraId, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarCadastrosFaciaisFracosQuery(obraId), ct));

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpGet("{id:guid}/assinatura/status-cadastro")]
    public async Task<IActionResult> ObterStatusCadastroBiometrico(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterStatusCadastroBiometricoQuery(id), ct));

    public record CadastrarBiometriaLocalRequestBody(byte[] TemplateBruto, byte[]? ImagemCadastro = null);

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/biometria-local/cadastro")]
    public async Task<IActionResult> CadastrarBiometriaLocal(Guid id, CadastrarBiometriaLocalRequestBody body, CancellationToken ct)
    {
        await _mediator.Send(new CadastrarTemplateBiometricoCommand(id, body.TemplateBruto, body.ImagemCadastro), ct);
        return NoContent();
    }

    public class CadastrarFacialRequestBody
    {
        public IFormFile Foto { get; set; } = null!;
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/facial/cadastro")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> CadastrarFacial(Guid id, [FromForm] CadastrarFacialRequestBody body, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await body.Foto.CopyToAsync(stream, ct);
        await _mediator.Send(new CadastrarFacialCommand(id, stream.ToArray()), ct);
        return NoContent();
    }

    public record RefazerCadastroFacialRequestBody(string Motivo);

    // Refazer o cadastro facial: mesma permissão do cadastro (técnico), limitado à obra do usuário no
    // handler. Remove o cadastro no Azure Face, arquiva as fotos e registra na trilha com o motivo.
    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpPost("{id:guid}/assinatura/facial/refazer")]
    public async Task<IActionResult> RefazerCadastroFacial(Guid id, RefazerCadastroFacialRequestBody body, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        await _mediator.Send(new RefazerCadastroFacialCommand(id, usuarioId, body.Motivo ?? string.Empty), ct);
        return NoContent();
    }

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpGet("{id:guid}/assinatura/facial/fotos")]
    public async Task<IActionResult> ListarFotosCadastroFacial(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarFotosCadastroFacialQuery(id), ct));

    [Authorize(Policy = "trabalhador:assinatura")]
    [HttpGet("{id:guid}/assinatura/facial/fotos/{fotoId:guid}")]
    public async Task<IActionResult> ObterFotoCadastroFacial(Guid id, Guid fotoId, CancellationToken ct)
    {
        var foto = await _mediator.Send(new ObterFotoCadastroFacialQuery(id, fotoId), ct);
        return foto is null ? NotFound() : File(foto.Conteudo, foto.ContentType, foto.NomeArquivo);
    }

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("{id:guid}/uniformes")]
    public async Task<IActionResult> ListarTamanhosUniforme(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarTamanhosUniformeTrabalhadorQuery(id), ct));

    [Authorize(Policy = "trabalhador:editar")]
    [HttpPut("{id:guid}/uniformes")]
    public async Task<IActionResult> DefinirTamanhosUniforme(Guid id, DefinirTamanhosUniformeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DefinirTamanhosUniformeTrabalhadorCommand(id, request.Itens), ct);
        return NoContent();
    }
}

public class AnexarFotoTrabalhadorRequestBody
{
    public IFormFile Foto { get; set; } = null!;
}

public record DefinirTamanhosUniformeRequest(List<ItemTamanhoUniforme> Itens);
