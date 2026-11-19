using System.Xml.Linq;
using AAHBRANT.SST.Infrastructure.Integracao.AgenteIa;

namespace AAHBRANT.SST.Infrastructure.Tests.Integracao;

public class AzureSpeechSinteseVozTests
{
    [Fact]
    public void Ssml_EscapaCaracteresDoTexto_E_UsaVozPtBr()
    {
        var ssml = AzureSpeechSinteseVoz.MontarSsml("Use EPI < 2 m & \"NR-35\" > altura", "pt-BR-FranciscaNeural", "+5%");

        // XML válido mesmo com <, >, & e aspas na resposta.
        var doc = XDocument.Parse(ssml);
        XNamespace ns = "http://www.w3.org/2001/10/synthesis";
        var voz = doc.Root!.Element(ns + "voice")!;
        Assert.Equal("pt-BR-FranciscaNeural", voz.Attribute("name")!.Value);
        Assert.Equal("Use EPI < 2 m & \"NR-35\" > altura", voz.Element(ns + "prosody")!.Value);
    }
}
