using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ideias;
using AAHBRANT.SST.Application.Ideias.Commands;
using AAHBRANT.SST.Application.Ideias.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AAHBRANT.SST.Api.Controllers;

// Banco de Ideias e Evolução do Produto (especificação do usuário, 08/10/2026).
// Permissões (§17): "ideia:usar" = qualquer usuário autenticado (registrar, consultar, comentar,
// anexar); "ideia:analisar" = Analista; "ideia:decidir" = Gestor; "ideia:desenvolver" = Tecnologia.
// Administrador recebe tudo pela matriz Perfil x Permissão (Controle de Acesso).
[ApiController]
[Route("api/ideias")]
public class IdeiasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _configuracao;
    private readonly IAuthorizationService _autorizacao;

    public IdeiasController(IMediator mediator, IAppDbContext db, IConfiguration configuracao, IAuthorizationService autorizacao)
    {
        _mediator = mediator;
        _db = db;
        _configuracao = configuracao;
        _autorizacao = autorizacao;
    }

    [Authorize(Policy = "ideia:usar")]
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busca, [FromQuery] StatusIdeia? status, [FromQuery] string? modulo,
        [FromQuery] string? categoria, [FromQuery] PrioridadeIdeia? prioridade, [FromQuery] string? responsavel,
        [FromQuery] string? criador, [FromQuery] DateTime? de, [FromQuery] DateTime? ate, CancellationToken ct)
        => Ok(await _mediator.Send(new ListarIdeiasQuery(busca, status, modulo, categoria, prioridade, responsavel, criador, de, ate), ct));

    [Authorize(Policy = "ideia:usar")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
        => Ok(await _mediator.Send(new DashboardIdeiasQuery(), ct));

    [Authorize(Policy = "ideia:usar")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ObterIdeiaQuery(id), ct));

    // Registro pela tela do aplicativo; o canal principal é o Telegram (TelegramIdeiasController).
    [Authorize(Policy = "ideia:usar")]
    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarIdeiaRequestBody body, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        var resultado = await _mediator.Send(new RegistrarIdeiaCommand(body.Mensagem, CanalIdeia.Web, autor), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id }, resultado);
    }

    [Authorize(Policy = "ideia:analisar")]
    [HttpPut("{id:guid}/analise")]
    public async Task<IActionResult> AtualizarAnalise(Guid id, AtualizarAnaliseIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new AtualizarAnaliseIdeiaCommand(
            id, b.Titulo, b.Descricao, b.ProblemaOportunidade, b.Objetivo, b.SolucaoSugerida, b.Modulo, b.Submodulo,
            b.Categoria, b.BeneficioEsperado, b.PossiveisImpactos, b.IntegracoesNecessarias, b.NecessidadeIa,
            b.Dependencias, b.InformacoesFaltantes, b.Impacto, b.Urgencia, b.Complexidade, b.Esforco, b.ValorNegocio,
            b.EsforcoEstimado, b.ViabilidadeTecnica, b.ViabilidadeOperacional, b.ResponsavelAnaliseUsuarioId,
            b.ResponsavelAnaliseNome, b.ResponsavelDesenvolvimentoUsuarioId, b.ResponsavelDesenvolvimentoNome,
            b.Observacoes, autor), ct));
    }

    // A permissão exigida depende do status de destino (FluxoStatusIdeia.PermissaoExigida).
    [Authorize(Policy = "ideia:usar")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusIdeiaRequestBody b, CancellationToken ct)
    {
        var permissao = FluxoStatusIdeia.PermissaoExigida(b.Destino);
        if (!(await _autorizacao.AuthorizeAsync(User, permissao)).Succeeded) return Forbid();

        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new AlterarStatusIdeiaCommand(id, b.Destino, b.Justificativa, b.Prioridade, autor), ct));
    }

    [Authorize(Policy = "ideia:usar")]
    [HttpPost("{id:guid}/comentarios")]
    public async Task<IActionResult> Comentar(Guid id, ComentarIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new ComentarIdeiaCommand(id, b.Texto, autor), ct));
    }

    [Authorize(Policy = "ideia:usar")]
    [HttpPost("{id:guid}/anexos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Anexar(Guid id, [FromForm] AnexarArquivoIdeiaRequestBody b, CancellationToken ct)
    {
        if (b.Arquivo is null || b.Arquivo.Length == 0) return BadRequest(new { erro = "Selecione um arquivo." });
        if (b.Arquivo.Length > AnexarArquivoIdeiaCommandValidator.TamanhoMaximoBytes)
            return BadRequest(new { erro = "O arquivo excede o limite de 5 MB." });

        using var ms = new MemoryStream();
        await b.Arquivo.CopyToAsync(ms, ct);
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new AnexarArquivoIdeiaCommand(id, b.Arquivo.FileName, b.Arquivo.ContentType, ms.ToArray(), autor), ct));
    }

    [Authorize(Policy = "ideia:usar")]
    [HttpGet("{id:guid}/anexos/{anexoId:guid}")]
    public async Task<IActionResult> BaixarAnexo(Guid id, Guid anexoId, CancellationToken ct)
    {
        var anexo = await _mediator.Send(new ObterAnexoIdeiaQuery(id, anexoId), ct);
        // nosniff + attachment: um HTML/SVG anexado nunca é executado no domínio do app.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(anexo.Conteudo, anexo.ContentType, anexo.NomeArquivo);
    }

    [Authorize(Policy = "ideia:analisar")]
    [HttpPost("{id:guid}/vincular")]
    public async Task<IActionResult> Vincular(Guid id, VincularIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new VincularIdeiaCommand(id, b.PrincipalId, autor), ct));
    }

    [Authorize(Policy = "ideia:analisar")]
    [HttpGet("{id:guid}/sugestoes")]
    public async Task<IActionResult> Sugestoes(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new SugerirAnaliseIdeiaQuery(id), ct));

    [Authorize(Policy = "ideia:analisar")]
    [HttpPost("{id:guid}/requisitos")]
    public async Task<IActionResult> CriarRequisito(Guid id, CriarRequisitoIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new CriarRequisitoIdeiaCommand(id, b.Titulo, b.Descricao, b.CriteriosAceite, autor), ct));
    }

    [Authorize(Policy = "ideia:decidir")]
    [HttpPost("requisitos/{requisitoId:guid}/aprovar")]
    public async Task<IActionResult> AprovarRequisito(Guid requisitoId, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new AprovarRequisitoIdeiaCommand(requisitoId, autor), ct));
    }

    [Authorize(Policy = "ideia:decidir")]
    [HttpPost("requisitos/{requisitoId:guid}/demanda")]
    public async Task<IActionResult> CriarDemanda(Guid requisitoId, CriarDemandaIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new CriarDemandaDesenvolvimentoCommand(
            requisitoId, b.Titulo, b.Descricao, b.ResponsavelUsuarioId, b.ResponsavelNome, autor), ct));
    }

    [Authorize(Policy = "ideia:desenvolver")]
    [HttpPut("demandas/{demandaId:guid}")]
    public async Task<IActionResult> AtualizarDemanda(Guid demandaId, AtualizarDemandaIdeiaRequestBody b, CancellationToken ct)
    {
        var autor = await ResolverAutorAsync(ct);
        return Ok(await _mediator.Send(new AtualizarDemandaDesenvolvimentoCommand(
            demandaId, b.Status, b.ResponsavelUsuarioId, b.ResponsavelNome, b.FuncionalidadeEntregue, autor), ct));
    }

    private async Task<AutorIdeia> ResolverAutorAsync(CancellationToken ct)
    {
        var azureAdObjectId = User.FindFirst("oid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;
        var nomeToken = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? email;

        if (!string.IsNullOrWhiteSpace(azureAdObjectId))
        {
            var u = await _db.Usuarios.Where(x => x.AzureAdObjectId == azureAdObjectId)
                .Select(x => new { x.Id, x.Nome }).FirstOrDefaultAsync(ct);
            if (u is not null) return new AutorIdeia(u.Id, u.Nome);
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var u = await _db.Usuarios.Where(x => x.Email == email)
                .Select(x => new { x.Id, x.Nome }).FirstOrDefaultAsync(ct);
            if (u is not null) return new AutorIdeia(u.Id, u.Nome);
        }

        // Sem Entra ID (desenvolvimento local) segue como "Usuário dev"; com Entra ID usa o nome do token.
        return new AutorIdeia(null, nomeToken ?? "Usuário dev");
    }
}

public class RegistrarIdeiaRequestBody
{
    public string Mensagem { get; set; } = string.Empty;
}

public class AtualizarAnaliseIdeiaRequestBody
{
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? ProblemaOportunidade { get; set; }
    public string? Objetivo { get; set; }
    public string? SolucaoSugerida { get; set; }
    public string? Modulo { get; set; }
    public string? Submodulo { get; set; }
    public string? Categoria { get; set; }
    public string? BeneficioEsperado { get; set; }
    public string? PossiveisImpactos { get; set; }
    public string? IntegracoesNecessarias { get; set; }
    public bool? NecessidadeIa { get; set; }
    public string? Dependencias { get; set; }
    public string? InformacoesFaltantes { get; set; }
    public NivelIdeia? Impacto { get; set; }
    public NivelIdeia? Urgencia { get; set; }
    public NivelIdeia? Complexidade { get; set; }
    public NivelIdeia? Esforco { get; set; }
    public NivelIdeia? ValorNegocio { get; set; }
    public string? EsforcoEstimado { get; set; }
    public ViabilidadeIdeia ViabilidadeTecnica { get; set; }
    public ViabilidadeIdeia ViabilidadeOperacional { get; set; }
    public Guid? ResponsavelAnaliseUsuarioId { get; set; }
    public string? ResponsavelAnaliseNome { get; set; }
    public Guid? ResponsavelDesenvolvimentoUsuarioId { get; set; }
    public string? ResponsavelDesenvolvimentoNome { get; set; }
    public string? Observacoes { get; set; }
}

public class AlterarStatusIdeiaRequestBody
{
    public StatusIdeia Destino { get; set; }
    public string? Justificativa { get; set; }
    public PrioridadeIdeia? Prioridade { get; set; }
}

public class ComentarIdeiaRequestBody
{
    public string Texto { get; set; } = string.Empty;
}

public class AnexarArquivoIdeiaRequestBody
{
    public IFormFile Arquivo { get; set; } = null!;
}

public class VincularIdeiaRequestBody
{
    public Guid PrincipalId { get; set; }
}

public class CriarRequisitoIdeiaRequestBody
{
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string CriteriosAceite { get; set; } = string.Empty;
}

public class CriarDemandaIdeiaRequestBody
{
    public string? Titulo { get; set; }
    public string? Descricao { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelNome { get; set; }
}

public class AtualizarDemandaIdeiaRequestBody
{
    public StatusDemandaDesenvolvimento Status { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelNome { get; set; }
    public string? FuncionalidadeEntregue { get; set; }
}
