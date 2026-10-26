using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Mapa corporal da lesão (24/10/2026): o JSON do mapa é do TeamsApp e a API o embute para o PDF.
public class MapaCorporalSvgTests
{
    // Se alguém gerar um código que o backend recusa, o técnico marcaria a região na tela e o registro
    // seria rejeitado ao salvar.
    [Fact]
    public void CodigosDoMapa_SaoAceitosPeloBackend()
    {
        Assert.Equal(2, MapaCorporalSvg.Desenhar(Array.Empty<string>()).Count); // frente e costas

        foreach (var codigo in new[] { "F:cabeca", "F:olho-esq", "F:mao-dir", "C:lombar-dir", "C:gluteo-esq", "F:dedos-pe-esq" })
        {
            Assert.True(MapaCorporalSvg.Existe(codigo), codigo);
            Assert.True(RegioesCorpoAcidente.CodigoValido(codigo), codigo);
        }
    }

    [Fact]
    public void Desenhar_MarcaSoAVistaDaRegiao()
    {
        var vistas = MapaCorporalSvg.Desenhar(new[] { "C:lombar-dir" });
        Assert.False(vistas.Single(v => v.Id == "F").TemMarcacao);
        Assert.True(vistas.Single(v => v.Id == "C").TemMarcacao);
        Assert.Contains("#C4262E", vistas.Single(v => v.Id == "C").Svg);
        Assert.DoesNotContain("#C4262E", vistas.Single(v => v.Id == "F").Svg);
        Assert.Equal("Região lombar (lado direito)", MapaCorporalSvg.Rotulo("C:lombar-dir"));
    }

    [Fact]
    public void Ordenar_SegueOrdemDoDesenhoERemoveRepetidos()
        => Assert.Equal(new[] { "F:olho-dir", "F:mao-esq", "C:lombar-dir" },
            MapaCorporalSvg.Ordenar(new[] { "C:lombar-dir", "F:mao-esq", "F:olho-dir", "F:mao-esq" }));
}
