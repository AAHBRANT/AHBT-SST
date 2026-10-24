using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Commands;

public record CriarSolicitacaoSuporteIaCommand(
    TipoSolicitacaoSuporteIa Tipo,
    SeveridadeSolicitacaoSuporteIa SeveridadeInformada,
    string Titulo,
    string Descricao,
    string? Modulo,
    string? UrlContexto,
    Guid? SolicitanteUsuarioId,
    string? SolicitanteNome,
    string? SolicitanteEmail) : IRequest<SuporteIaSolicitacaoDto>;

public class CriarSolicitacaoSuporteIaCommandValidator : AbstractValidator<CriarSolicitacaoSuporteIaCommand>
{
    public CriarSolicitacaoSuporteIaCommandValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Modulo).MaximumLength(120);
        RuleFor(x => x.UrlContexto).MaximumLength(800);
        RuleFor(x => x.SolicitanteNome).MaximumLength(160);
        RuleFor(x => x.SolicitanteEmail).MaximumLength(256);
    }
}

public class CriarSolicitacaoSuporteIaCommandHandler : IRequestHandler<CriarSolicitacaoSuporteIaCommand, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;
    private readonly ISuporteIaTriagemService _triagem;
    private readonly ITelegramSuporteService _telegram;
    private readonly IFilaCalendarioTeams _filaCalendario;
    private readonly IFilaNotificacaoTeams _filaTeams;
    private readonly ISuporteIaConfiguracao _configuracao;

    public CriarSolicitacaoSuporteIaCommandHandler(
        IAppDbContext db,
        ISuporteIaTriagemService triagem,
        ITelegramSuporteService telegram,
        IFilaNotificacaoTeams filaTeams,
        IFilaCalendarioTeams filaCalendario,
        ISuporteIaConfiguracao configuracao)
    {
        _db = db;
        _triagem = triagem;
        _telegram = telegram;
        _filaCalendario = filaCalendario;
        _filaTeams = filaTeams;
        _configuracao = configuracao;
    }

    public async Task<SuporteIaSolicitacaoDto> Handle(CriarSolicitacaoSuporteIaCommand request, CancellationToken ct)
    {
        var triagem = await _triagem.TriarAsync(new SuporteIaEntradaTriagem(
            request.Tipo.ToString(),
            request.SeveridadeInformada.ToString(),
            request.Titulo,
            request.Descricao,
            request.Modulo,
            request.UrlContexto), ct);

        var agora = DateTime.UtcNow;
        var solicitacao = new SuporteIaSolicitacao
        {
            Tipo = request.Tipo,
            SeveridadeInformada = request.SeveridadeInformada,
            Status = triagem.RequerAlteracaoCodigo ? StatusSolicitacaoSuporteIa.Encaminhada : StatusSolicitacaoSuporteIa.Respondida,
            Titulo = request.Titulo.Trim(),
            Descricao = request.Descricao.Trim(),
            Modulo = string.IsNullOrWhiteSpace(request.Modulo) ? null : request.Modulo.Trim(),
            UrlContexto = string.IsNullOrWhiteSpace(request.UrlContexto) ? null : request.UrlContexto.Trim(),
            SolicitanteUsuarioId = request.SolicitanteUsuarioId,
            SolicitanteNome = string.IsNullOrWhiteSpace(request.SolicitanteNome) ? null : request.SolicitanteNome.Trim(),
            SolicitanteEmail = string.IsNullOrWhiteSpace(request.SolicitanteEmail) ? null : request.SolicitanteEmail.Trim(),
            ResultadoTriagem = triagem.Resultado,
            RequerAlteracaoCodigo = triagem.RequerAlteracaoCodigo,
            RespostaAoUsuario = triagem.RespostaAoUsuario,
            DemandaReduzida = triagem.DemandaReduzida,
            SolucaoProposta = triagem.SolucaoProposta,
            EvidenciasTecnicas = triagem.EvidenciasTecnicas,
            TriadoEmUtc = agora,
            EncaminhadoEmUtc = triagem.RequerAlteracaoCodigo ? agora : null
        };

        _db.SuporteIaSolicitacoes.Add(solicitacao);

        // Todo chamado avisa o responsável no sininho do Teams (pedido do usuário, 24/09/2026) — antes
        // só a demanda técnica avisava, e dúvida respondida pela IA chegava apenas no Telegram. O
        // chamado já respondido pela IA nasce com o alerta resolvido: avisa, mas não fica pendente
        // no contador de alertas abertos.
        if (TryObterResponsavel(out var responsavelId))
        {
            var alerta = new Alerta
            {
                Tipo = TipoAlerta.SuporteIaDemandaTecnica,
                Severidade = !triagem.RequerAlteracaoCodigo
                    ? SeveridadeAlerta.Info
                    : request.SeveridadeInformada >= SeveridadeSolicitacaoSuporteIa.Alta ? SeveridadeAlerta.Critico : SeveridadeAlerta.Atencao,
                Status = triagem.RequerAlteracaoCodigo ? StatusAlerta.Aberto : StatusAlerta.Resolvido,
                Titulo = $"Suporte IA: {request.Titulo.Trim()}",
                Descricao = triagem.RequerAlteracaoCodigo ? triagem.DemandaReduzida : triagem.RespostaAoUsuario,
                EntidadeOrigemTipo = nameof(SuporteIaSolicitacao),
                EntidadeOrigemId = solicitacao.Id,
                DestinatarioUsuarioId = responsavelId
            };
            _db.Alertas.Add(alerta);
            solicitacao.AlertaId = alerta.Id;
        }

        await _db.SaveChangesAsync(ct);

        await NotificarTelegramAsync(solicitacao, ct);
        await NotificarTeamsAsync(solicitacao, ct);
        await CriarEventoCalendarioAsync(solicitacao, agora, ct);

        return Mapear(solicitacao);
    }

    private async Task NotificarTelegramAsync(SuporteIaSolicitacao solicitacao, CancellationToken ct)
    {
        var mensagem = MontarMensagemTelegram(solicitacao);
        try
        {
            await _telegram.EnviarDemandaAsync(mensagem, ct);
        }
        catch
        {
            // A solicitação já foi persistida; falha em canal externo não deve quebrar o retorno ao usuário.
        }
    }

    // Evento de dia inteiro no calendário do responsável, na data do prazo de atendimento pela
    // severidade (ver SuporteIaCalendario.CalcularPrazo). Cancelado quando o chamado é encerrado.
    private async Task CriarEventoCalendarioAsync(SuporteIaSolicitacao solicitacao, DateTime agoraUtc, CancellationToken ct)
    {
        if (solicitacao.AlertaId.HasValue && TryObterResponsavel(out var responsavelId))
        {
            try
            {
                await _filaCalendario.EnfileirarAsync(new CalendarioTeamsMensagem(
                    AlertaEngineService.OrigemCalendarioAlerta,
                    solicitacao.AlertaId.Value,
                    OperacaoCalendarioTeams.Criar,
                    responsavelId,
                    $"Suporte IA: {solicitacao.Titulo}",
                    solicitacao.RequerAlteracaoCodigo ? solicitacao.DemandaReduzida : solicitacao.RespostaAoUsuario,
                    SuporteIaCalendario.CalcularPrazo(solicitacao.SeveridadeInformada, agoraUtc)), ct);
            }
            catch
            {
                // Mesmo princípio do Telegram: o registro da demanda é a fonte de verdade.
            }
        }
    }

    private async Task NotificarTeamsAsync(SuporteIaSolicitacao solicitacao, CancellationToken ct)
    {
        if (solicitacao.AlertaId.HasValue && TryObterResponsavel(out var responsavelId))
        {
            try
            {
                await _filaTeams.EnfileirarAsync(new NotificacaoTeamsMensagem(
                    solicitacao.AlertaId.Value,
                    responsavelId,
                    $"Suporte IA: {solicitacao.Titulo}",
                    solicitacao.RequerAlteracaoCodigo ? solicitacao.DemandaReduzida : solicitacao.RespostaAoUsuario), ct);
            }
            catch
            {
                // Mesmo princípio do Telegram: o registro da demanda é a fonte de verdade.
            }
        }
    }

    private bool TryObterResponsavel(out Guid responsavelId)
    {
        var id = _configuracao.ResponsavelUsuarioId ?? Guid.Empty;
        if (id != Guid.Empty && _db.Usuarios.Any(u => u.Id == id))
        {
            responsavelId = id;
            return true;
        }

        var email = _configuracao.ResponsavelEmail;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var usuarioId = _db.Usuarios
                .Where(u => u.Email == email)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefault();
            if (usuarioId.HasValue)
            {
                responsavelId = usuarioId.Value;
                return true;
            }
        }

        responsavelId = Guid.Empty;
        return false;
    }

    private static string MontarMensagemTelegram(SuporteIaSolicitacao solicitacao)
        => $"""
           Nova solicitação na Central de Suporte IA SST

           Tipo: {solicitacao.Tipo}
           Severidade: {solicitacao.SeveridadeInformada}
           Status: {solicitacao.Status}
           Triagem: {solicitacao.ResultadoTriagem}
           Módulo: {solicitacao.Modulo ?? "não informado"}
           Solicitante: {solicitacao.SolicitanteNome ?? "não informado"} {solicitacao.SolicitanteEmail}

           Pedido do cliente:
           {solicitacao.Titulo}

           Descrição:
           {solicitacao.Descricao}

           Resposta ao usuário:
           {solicitacao.RespostaAoUsuario}

           Demanda reduzida:
           {(string.IsNullOrWhiteSpace(solicitacao.DemandaReduzida) ? "não aplicável" : solicitacao.DemandaReduzida)}

           Solução proposta:
           {(string.IsNullOrWhiteSpace(solicitacao.SolucaoProposta) ? "não aplicável" : solicitacao.SolucaoProposta)}

           Contexto:
           {solicitacao.UrlContexto ?? "não informado"}
           """;

    private static SuporteIaSolicitacaoDto Mapear(SuporteIaSolicitacao s) => new()
    {
        Id = s.Id,
        Tipo = s.Tipo,
        SeveridadeInformada = s.SeveridadeInformada,
        Status = s.Status,
        Titulo = s.Titulo,
        Descricao = s.Descricao,
        Modulo = s.Modulo,
        UrlContexto = s.UrlContexto,
        SolicitanteUsuarioId = s.SolicitanteUsuarioId,
        SolicitanteNome = s.SolicitanteNome,
        SolicitanteEmail = s.SolicitanteEmail,
        ResultadoTriagem = s.ResultadoTriagem,
        RequerAlteracaoCodigo = s.RequerAlteracaoCodigo,
        RespostaAoUsuario = s.RespostaAoUsuario,
        DemandaReduzida = s.DemandaReduzida,
        SolucaoProposta = s.SolucaoProposta,
        EvidenciasTecnicas = s.EvidenciasTecnicas,
        CreatedAtUtc = s.CreatedAtUtc,
        TriadoEmUtc = s.TriadoEmUtc,
        EncaminhadoEmUtc = s.EncaminhadoEmUtc,
        // Quem acabou de criar o chamado é, por definição, o solicitante — sem precisar comparar
        // identidade como nas outras queries/comandos, que recebem a entidade já persistida sem
        // saber quem está chamando.
        SouSolicitante = true
    };
}
