namespace AAHBRANT.SST.Application.Calendario;

/// <summary>Mensagem pública sem o corpo técnico da resposta do Microsoft Graph.</summary>
public class CalendarioTeamsIndisponivelException(string mensagem, Exception causa)
    : InvalidOperationException(mensagem, causa);
