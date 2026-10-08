using System.Text.Json;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Application.Tests.Common;

public class DadosCapturaFotoTests
{
    [Fact]
    public void ExigirCompleta_ComCapturaEPosicaoRecentes_NaoLanca()
    {
        var captura = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(new DadosCapturaFoto
        {
            CapturadaEm = captura,
            FusoMinutos = 180,
            Origem = "camera",
            Local = "Galpão 2",
            ObraId = Guid.NewGuid(),
            ObraNome = "Obra Centro",
            Latitude = -23.55,
            Longitude = -46.63,
            PrecisaoMetros = 12,
            LocalizacaoObtidaEm = captura.AddSeconds(-4),
        });

        DadosCapturaFoto.ExigirCompleta(json, "Foto 1");
    }

    [Fact]
    public void ExigirCompleta_FotoDaGaleriaComObraELocal_NaoExigeDataNemGeolocalizacao()
    {
        var json = JsonSerializer.Serialize(new DadosCapturaFoto
        {
            Origem = "arquivo",
            Local = "Pátio",
            ObraId = Guid.NewGuid(),
            ObraNome = "Obra Centro",
        });

        DadosCapturaFoto.ExigirCompleta(json, "Foto 3");
    }

    [Fact]
    public void ExigirCompleta_FotoDaGaleriaSemLocal_NaoExigeDescricaoDoLocal()
    {
        var json = JsonSerializer.Serialize(new DadosCapturaFoto
        {
            Origem = "arquivo",
            ObraId = Guid.NewGuid(),
            ObraNome = "Obra Centro",
        });

        DadosCapturaFoto.ExigirCompleta(json, "Foto 3");
    }

    [Fact]
    public void Pendencias_FotoDaCameraSemLocal_NaoListaDescricaoDoLocal()
    {
        var captura = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(new DadosCapturaFoto
        {
            Origem = "camera",
            CapturadaEm = captura,
            ObraId = Guid.NewGuid(),
            ObraNome = "Obra Centro",
            Latitude = -7.1,
            Longitude = -34.8,
            PrecisaoMetros = 12,
            LocalizacaoObtidaEm = captura.AddSeconds(-4),
        });

        Assert.Empty(DadosCapturaFoto.Pendencias(json));
    }

    [Fact]
    public void ExigirCompleta_SemLocalizacao_InformaPendenciaSemUsarHorarioDeRecebimento()
    {
        var json = JsonSerializer.Serialize(new DadosCapturaFoto
        {
            CapturadaEm = DateTimeOffset.UtcNow,
            FusoMinutos = 180,
            Origem = "camera",
            Local = "Pátio",
            ObraId = Guid.NewGuid(),
            ObraNome = "Obra Centro",
            RecebidaEm = DateTimeOffset.UtcNow,
        });

        var ex = Assert.Throws<InvalidOperationException>(() => DadosCapturaFoto.ExigirCompleta(json, "Foto 2"));

        Assert.Contains("geolocalização da captura", ex.Message);
    }
}
