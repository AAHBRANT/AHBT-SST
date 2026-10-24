namespace AAHBRANT.SST.Application.Common;

// Serviço externo (ex.: Azure Face) fora do ar ou com cota estourada. Vira HTTP 503: é temporário, então a
// fila offline do app tenta de novo depois em vez de descartar a ação como erro definitivo (um 400 era
// tratado como "não adianta repetir" e a foto da assinatura se perdia). A mensagem é para a pessoa; a causa
// técnica vai em InnerException, para o log.
public sealed class ServicoIndisponivelException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);
