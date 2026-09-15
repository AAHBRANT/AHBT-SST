using AAHBRANT.SST.Application.Trabalhadores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAHBRANT.SST.Api.Controllers;

/// <summary>
/// Canal novo, unidirecional G-RH -> SST (15/09/2026): o G-RH ainda não tem foto de ninguém,
/// então busca aqui pra quem já foi fotografado no cadastro de EPI/ASO do SST, por CPF.
///
/// Mesma policy de <see cref="TrabalhadoresController"/> ("trabalhador:ver") — o token
/// client-credentials do G-RH carrega a App Role <c>Sst.LerFotos</c>, reconhecida em
/// <see cref="Api.Autorizacao.AppRolesReconhecidas"/> (mesmo mecanismo já usado por
/// <c>Grh.LerColaboradores</c>), que dá acesso global via
/// <see cref="Middlewares.EscopoPorObraMiddleware"/> sem precisar de nenhum código especial
/// aqui.
/// </summary>
[ApiController]
[Route("api/integracoes/grh")]
public class IntegracaoGrhController : ControllerBase
{
    private readonly IMediator _mediator;

    public IntegracaoGrhController(IMediator mediator) => _mediator = mediator;

    [Authorize(Policy = "trabalhador:ver")]
    [HttpGet("trabalhadores/{cpf}/foto")]
    public async Task<IActionResult> ObterFotoPorCpf(string cpf, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11 || !cpf.All(char.IsDigit))
        {
            return BadRequest(new { erro = "Informe o CPF com 11 dígitos, sem pontuação" });
        }

        var foto = await _mediator.Send(new ObterFotoTrabalhadorPorCpfQuery(cpf), ct);
        if (foto is null) return NotFound();

        return File(foto.Conteudo, foto.ContentType);
    }
}
