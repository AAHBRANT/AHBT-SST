using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Calendario.Queries;

// "Quero o calendário dentro do aplicativo, tem que ser o Teams" (requisito do usuário, 2026-08-29)
// — combina os eventos reais do Outlook/Teams do usuário logado (via Microsoft Graph) com os
// vencimentos que o próprio Motor de Alertas já gera para ele (ASO, treinamento, EPI etc.),
// filtrados no mesmo intervalo de datas. AzureAdObjectId vem do claim "oid" do token (resolvido no
// controller, mesmo padrão de AssinarComSessaoLogadaCommand) — não do ICurrentUserService, que só
// expõe o escopo por obra, não o usuário em si.
public record ObterCalendarioQuery(string? AzureAdObjectId, DateTime Inicio, DateTime Fim) : IRequest<CalendarioDto>;

public class ObterCalendarioQueryHandler : IRequestHandler<ObterCalendarioQuery, CalendarioDto>
{
    private readonly IAppDbContext _db;
    private readonly ICalendarioTeamsService _graph;
    private readonly ILogger<ObterCalendarioQueryHandler> _logger;

    public ObterCalendarioQueryHandler(IAppDbContext db, ICalendarioTeamsService graph, ILogger<ObterCalendarioQueryHandler> logger)
    {
        _db = db;
        _graph = graph;
        _logger = logger;
    }

    public async Task<CalendarioDto> Handle(ObterCalendarioQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AzureAdObjectId))
            return new CalendarioDto(false, false, null, new List<EventoGraphDto>(), new List<EventoSstDto>());

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            return new CalendarioDto(false, false, null, new List<EventoGraphDto>(), new List<EventoSstDto>());

        var eventosSst = await _db.Alertas
            .Where(a => a.DestinatarioUsuarioId == usuario.Id
                && a.DataLimiteTratamento != null
                && a.DataLimiteTratamento >= request.Inicio
                && a.DataLimiteTratamento <= request.Fim)
            .OrderBy(a => a.DataLimiteTratamento)
            .Select(a => new EventoSstDto(
                a.Id, a.Titulo, a.Descricao, a.DataLimiteTratamento!.Value, a.Tipo, a.Severidade, a.Status,
                a.EntidadeOrigemTipo, a.EntidadeOrigemId))
            .ToListAsync(ct);

        // Falha na leitura do Graph (permissão ainda não concedida, usuário sem AzureAdObjectId —
        // não deveria acontecer aqui já que acabamos de resolvê-lo, mas o serviço confere de novo —
        // ou token/rede) nunca esconde os vencimentos do SST: degrada para "só o lado SST" com uma
        // mensagem explicando o motivo, em vez de estourar a tela inteira.
        try
        {
            var eventosGraph = await _graph.ListarEventosAsync(usuario.Id, request.Inicio, request.Fim, ct);
            return new CalendarioDto(true, true, null, eventosGraph.ToList(), eventosSst);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler o calendário do Teams/Outlook do usuário {UsuarioId} via Graph.", usuario.Id);
            return new CalendarioDto(true, false, ex.Message, new List<EventoGraphDto>(), eventosSst);
        }
    }
}
