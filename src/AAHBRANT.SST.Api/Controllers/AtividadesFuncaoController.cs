using AAHBRANT.SST.Api.Autorizacao;
using AAHBRANT.SST.Application.Ghes.AtividadesFuncao;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

// Atividades por função na OS (Entrega 4, 10/11/2026): a IA sugere quais atividades do GHE cada função
// executa e o Engenheiro de Segurança confirma; a OS passa a listar só as confirmadas. Quem decidiu vem
// sempre do token. Sem DELETE (descartar não apaga) e sem endpoint anônimo.
[ApiController]
[Route("api/obras/{obraId:guid}")]
public class AtividadesFuncaoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUsuarioAtualResolver _usuarioAtual;

    public AtividadesFuncaoController(IMediator mediator, IUsuarioAtualResolver usuarioAtual)
    {
        _mediator = mediator;
        _usuarioAtual = usuarioAtual;
    }

    public record DecidirAtividadesFuncaoRequest(List<DecidirAtividadeFuncaoItem> Itens);

    public record DecidirAtividadeFuncaoItem(Guid AtividadeId, Guid FuncaoId, bool Executa);

    [Authorize(Policy = "risco:ver")]
    [HttpGet("ghes/{gheId:guid}/atividades-funcao")]
    public async Task<IActionResult> ObterMatriz(Guid obraId, Guid gheId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new ObterMatrizAtividadesFuncaoGheQuery(obraId, gheId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Policy = "risco:editar")]
    [HttpPost("ghes/{gheId:guid}/atividades-funcao/sugerir-com-ia")]
    public async Task<IActionResult> SugerirComIa(Guid obraId, Guid gheId, CancellationToken ct)
        => Ok(await _mediator.Send(new SugerirAtividadesFuncaoGheCommand(obraId, gheId), ct));

    [Authorize(Policy = PoliticasAutorizacao.SomenteEngenheiroSeguranca)]
    [HttpPost("atividades-funcao/decidir")]
    public async Task<IActionResult> Decidir(Guid obraId, DecidirAtividadesFuncaoRequest corpo, CancellationToken ct)
    {
        var usuarioId = await _usuarioAtual.ObterIdAsync(User, ct);
        var itens = (corpo.Itens ?? new()).Select(i => new DecisaoAtividadeFuncao(i.AtividadeId, i.FuncaoId, i.Executa)).ToList();
        var decididos = await _mediator.Send(new DecidirAtividadesFuncaoCommand(obraId, itens, usuarioId, User.FindFirst("name")?.Value), ct);
        return Ok(new { decididos });
    }
}
