namespace AAHBRANT.SST.Application.Terceirizados.Portal;

public static class PortalTerceiroRegras
{
    public static readonly TimeSpan ValidadeCodigo = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan IntervaloMinimoEntreCodigos = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ValidadeSessao = TimeSpan.FromHours(8);
    public const int MaximoTentativasCodigo = 5;

    // Link vale até o fim da vigência do contrato (+ folga para enviar documentos de quem sai), e
    // nunca menos que isto — contrato com vigência já curta ainda dá tempo de a empresa se organizar.
    public static readonly TimeSpan ValidadeMinimaLink = TimeSpan.FromDays(30);
    public static readonly TimeSpan FolgaAposVigencia = TimeSpan.FromDays(15);

    // Versão do termo de uso/tratamento de dados exibido no portal. Mudou o texto → nova versão, e o
    // portal volta a pedir o aceite. O texto é provisório até a validação do jurídico.
    public const string VersaoTermoAtual = "2026-10-18";

    public static readonly string[] TiposArquivoPermitidos = { "application/pdf", "image/jpeg", "image/png" };
    public const int TamanhoMaximoArquivoBytes = 10 * 1024 * 1024;
}
