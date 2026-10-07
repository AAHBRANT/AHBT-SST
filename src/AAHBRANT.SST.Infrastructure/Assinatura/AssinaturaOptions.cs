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

    // Fila facial (identificação 1:N sem escolher a pessoa): o melhor candidato precisa estar pelo menos
    // esta diferença (0 a 1) à frente do segundo, senão a leitura é recusada como ambígua e o
    // operador usa a digital. Evita confirmar o colega parecido.
    public double MargemMinimaIdentificacaoFacial { get; set; } = 0.10;
}
