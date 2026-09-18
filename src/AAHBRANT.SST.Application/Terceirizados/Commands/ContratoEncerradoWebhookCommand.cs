using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record ContratoEncerradoWebhookCommand(string GJuriContratoId, DateOnly DataEncerramento) : IRequest;

public class ContratoEncerradoWebhookCommandValidator : AbstractValidator<ContratoEncerradoWebhookCommand>
{
    public ContratoEncerradoWebhookCommandValidator()
    {
        RuleFor(x => x.GJuriContratoId).NotEmpty();
    }
}

public class ContratoEncerradoWebhookCommandHandler : IRequestHandler<ContratoEncerradoWebhookCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicosSegurancaPorObra;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public ContratoEncerradoWebhookCommandHandler(
        IAppDbContext db, ITecnicosSegurancaPorObraService tecnicosSegurancaPorObra, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _tecnicosSegurancaPorObra = tecnicosSegurancaPorObra;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task Handle(ContratoEncerradoWebhookCommand request, CancellationToken ct)
    {
        var contrato = await _db.Contratos.FirstOrDefaultAsync(c => c.GJuriContratoId == request.GJuriContratoId, ct)
            ?? throw new KeyNotFoundException($"Contrato com GJuriContratoId '{request.GJuriContratoId}' não encontrado.");

        if (contrato.Status == StatusContrato.Encerrado)
            return; // idempotente — evento repetido do G-Juri não gera alerta duplicado.

        contrato.Status = StatusContrato.Encerrado;

        var pessoasAtivas = await _db.Trabalhadores
            .Where(t => t.ContratoId == contrato.Id && t.DataDemissao == null)
            .ToListAsync(ct);

        var alertasCriados = new List<Alerta>();
        if (pessoasAtivas.Count > 0)
        {
            var tecnicos = await _tecnicosSegurancaPorObra.ObterUsuarioIdsAsync(contrato.ObraId, ct);
            foreach (var usuarioId in tecnicos)
            {
                var alerta = new Alerta
                {
                    Tipo = TipoAlerta.ContratoTerceirizadoEncerrado,
                    Severidade = SeveridadeAlerta.Atencao,
                    Titulo = $"Contrato {contrato.NumeroContrato} encerrado com {pessoasAtivas.Count} pessoa(s) ainda ativa(s)",
                    Descricao = "Revise se essas pessoas devem ser desligadas no SST — o encerramento do contrato no G-Juri não desliga ninguém automaticamente.",
                    EntidadeOrigemTipo = "Contrato",
                    EntidadeOrigemId = contrato.Id,
                    ObraId = contrato.ObraId,
                    DestinatarioUsuarioId = usuarioId,
                };
                _db.Alertas.Add(alerta);
                alertasCriados.Add(alerta);
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var alerta in alertasCriados)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }
    }
}
