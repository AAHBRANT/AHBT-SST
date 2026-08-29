using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Calendario;

// Vencimento do Motor de Alertas (ASO, treinamento, EPI, inspeção etc.) exibido como evento no
// calendário do app — usa Alerta.DataLimiteTratamento como a data do evento. Independe de o Graph
// ter conseguido criar o evento espelhado no Outlook (CalendarioEventoTeams) — mesmo sem essa
// sincronização (ou com ela falhando), o vencimento continua visível dentro do app.
public record EventoSstDto(
    Guid AlertaId,
    string Titulo,
    string? Descricao,
    DateTime Data,
    TipoAlerta Tipo,
    SeveridadeAlerta Severidade,
    StatusAlerta Status,
    string EntidadeOrigemTipo,
    Guid EntidadeOrigemId);

// Combina os dois lados pedidos pelo usuário (2026-08-29): "quero o calendário dentro do
// aplicativo, tem que ser o Teams" + vencimentos do próprio SST sobrepostos. GraphDisponivel=false
// quando a leitura do Graph falha (permissão não concedida, usuário sem AzureAdObjectId etc.) —
// nesse caso EventosGraph vem vazio e MensagemErroGraph explica o motivo, mas EventosSst continua
// populado normalmente (degradação graciosa, não é tudo-ou-nada).
public record CalendarioDto(
    bool UsuarioIdentificado,
    bool GraphDisponivel,
    string? MensagemErroGraph,
    List<EventoGraphDto> EventosGraph,
    List<EventoSstDto> EventosSst);
