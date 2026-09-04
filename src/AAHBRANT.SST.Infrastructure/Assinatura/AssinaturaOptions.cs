namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class AssinaturaOptions
{
    public string UrlBaseValidacaoPublica { get; set; } = "";
    public double LimiarConfiancaBiometriaLocal { get; set; } = 50;

    // Azure Face API (docs/superpowers/specs/2026-09-04-assinatura-facial-azure-design.md) — tier F0
    // (gratuito): 20 chamadas/minuto, até 30.000 rostos. Migrar para S0 é só trocar a chave.
    public string AzureFaceApiEndpoint { get; set; } = "";
    public string AzureFaceApiKey { get; set; } = "";
    public double LimiarConfiancaFacial { get; set; } = 0.85;
    public double LimiarConfiancaFacialMinimo { get; set; } = 0.60;
}
