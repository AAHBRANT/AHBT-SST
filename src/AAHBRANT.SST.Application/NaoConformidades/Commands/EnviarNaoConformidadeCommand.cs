using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.3/§10) — botão ENVIAR: "representa o
// encaminhamento formal da inspeção [ocorrência]. O inspetor não deve utilizar a função de
// encerramento nesta etapa." A plataforma deve notificar o responsável quando enviada (§8) — feito
// aqui de forma imediata, criando um Alerta real (TipoAlerta.NaoConformidadeAberta, já reservado no
// enum) e enfileirando a notificação Teams com o Id desse Alerta — NotificacaoTeamsMensagem.AlertaId
// tem FK real para a tabela Alertas (via AlertaHistoricoEnvio), então não pode carregar o Id da NC
// diretamente; mesmo padrão de CriarAlertaCommand (criação manual de alerta).
public record EnviarNaoConformidadeCommand(Guid Id) : IRequest;

public class EnviarNaoConformidadeCommandValidator : AbstractValidator<EnviarNaoConformidadeCommand>
{
    public EnviarNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EnviarNaoConformidadeCommandHandler : IRequestHandler<EnviarNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public EnviarNaoConformidadeCommandHandler(IAppDbContext db, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task Handle(EnviarNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades
            .Include(n => n.Atividade)
            .FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        if (nc.Status != StatusNaoConformidade.Aberta)
            throw new InvalidOperationException("Só é possível enviar uma ocorrência que esteja Aberta.");

        if (!nc.ResponsavelUsuarioId.HasValue)
            throw new InvalidOperationException("Defina o responsável pela tratativa antes de enviar.");

        nc.Status = StatusNaoConformidade.Enviada;

        var alerta = new Alerta
        {
            Tipo = TipoAlerta.NaoConformidadeAberta,
            Severidade = SeveridadeAlerta.Atencao,
            Titulo = "Nova ocorrência de inspeção encaminhada a você",
            Descricao = nc.Descricao,
            EntidadeOrigemTipo = nameof(NaoConformidade),
            EntidadeOrigemId = nc.Id,
            ObraId = nc.Atividade?.ObraId,
            DestinatarioUsuarioId = nc.ResponsavelUsuarioId,
        };
        _db.Alertas.Add(alerta);

        await _db.SaveChangesAsync(ct);

        await _filaNotificacaoTeams.EnfileirarAsync(
            new NotificacaoTeamsMensagem(alerta.Id, nc.ResponsavelUsuarioId.Value, alerta.Titulo, alerta.Descricao),
            ct);
    }
}
