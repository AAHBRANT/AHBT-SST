using System.Globalization;
using AAHBRANT.SST.Infrastructure.Assinatura;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

// Guardião do grupo facial (23/11): depois da 2ª quebra na Beira Rio, o grupo de cada obra passa a ser
// conferido toda noite (não só até a migração terminar) e na hora em que uma falha suspeita aparece.
public class GuardiaoGrupoFacialTests
{
    [Fact]
    public void Fila_MesmaObraEmSequencia_SoEntraUmaVezNoIntervalo()
    {
        var agora = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var fila = new FilaReparoGrupoFacial(() => agora);
        var obra = Guid.NewGuid();

        fila.Solicitar(obra);
        fila.Solicitar(obra);
        agora = agora.AddMinutes(10);
        fila.Solicitar(obra);

        Assert.True(fila.TentarRetirar(out var primeira));
        Assert.Equal(obra, primeira);
        Assert.False(fila.TentarRetirar(out _));
    }

    [Fact]
    public void Fila_DepoisDoIntervalo_AceitaAMesmaObraDeNovo()
    {
        var agora = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var fila = new FilaReparoGrupoFacial(() => agora);
        var obra = Guid.NewGuid();

        fila.Solicitar(obra);
        Assert.True(fila.TentarRetirar(out _));
        agora = agora.Add(FilaReparoGrupoFacial.IntervaloMinimoPorObra).AddMinutes(1);
        fila.Solicitar(obra);

        Assert.True(fila.TentarRetirar(out _));
    }

    [Fact]
    public void Fila_ObrasDiferentes_EntramAsDuas()
    {
        var fila = new FilaReparoGrupoFacial(() => DateTime.UtcNow);
        fila.Solicitar(Guid.NewGuid());
        fila.Solicitar(Guid.NewGuid());

        Assert.True(fila.TentarRetirar(out _));
        Assert.True(fila.TentarRetirar(out _));
    }

    // Antes o serviço noturno TERMINAVA depois que todas as obras migravam: nunca mais conferia nada. Agora
    // faz uma passada completa por noite, dentro da janela (22h às 5h de Brasília).
    [Theory]
    [InlineData("2026-11-24T02:00:00Z", null, true)]                    // 23h BRT, nunca conferiu
    [InlineData("2026-11-24T02:00:00Z", "2026-11-24T01:30:00Z", false)] // já conferiu nesta noite (22h30)
    [InlineData("2026-11-24T07:00:00Z", "2026-11-23T02:00:00Z", true)]  // 4h BRT, última foi na noite anterior
    [InlineData("2026-11-24T15:00:00Z", null, false)]                   // 12h BRT, fora da janela
    public void Noturno_UmaPassadaCompletaPorNoite(string agora, string? ultima, bool esperado)
    {
        var agoraUtc = DateTime.Parse(agora, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        DateTime? ultimaUtc = ultima is null ? null : DateTime.Parse(ultima, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(esperado, MigracaoModeloFacialBackgroundService.DeveConferir(agoraUtc, ultimaUtc, inicio: 22, fim: 5));
    }
}
