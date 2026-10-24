namespace AAHBRANT.SST.Domain.Enums;

// Situação da geolocalização informada pelo aparelho no momento da assinatura (log de assinaturas
// da Ficha de EPI). Nunca fica em branco: quando o usuário nega a permissão ou o aparelho não tem
// posição, o log mostra isso explicitamente em vez de omitir o campo.
public enum StatusLocalizacaoAssinatura
{
    // Assinatura anterior à captura de localização, ou cliente que não enviou nada.
    NaoInformada = 0,
    Capturada = 1,
    NaoAutorizada = 2,
    Indisponivel = 3,
}
