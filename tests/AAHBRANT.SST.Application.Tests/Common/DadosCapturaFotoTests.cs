using System.Text.Json;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

public class DadosCapturaFotoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"origem\":\"arquivo\"}")]
    [InlineData("{\"origem\":\"desconhecida\"}")]
    [InlineData("invalido")]
    public async Task NovaEvidencia_RecusaEnvioSemCaptura(string? json)
    {
        using var db = CriarBanco();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DadosCapturaFoto.PrepararAsync(json, Guid.NewGuid(), Guid.NewGuid(), db, default));
    }

    [Fact]
    public async Task NovaEvidencia_ValidaRegistroObraEPosicaoMesmoNaSincronizacaoOffline()
    {
        using var db = CriarBanco();
        var obra = new Obra { Nome = "Obra Centro", Codigo = "CAM" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var registro = Guid.NewGuid();
        var hora = DateTimeOffset.UtcNow.AddDays(-2);
        var dados = new DadosCapturaFoto { Origem = "camera", RegistroId = registro, ObraId = obra.Id,
            ObraNome = "Nome no aparelho", CapturadaEm = hora, Latitude = -23, Longitude = -46,
            PrecisaoMetros = 12, LocalizacaoObtidaEm = hora };
        Task<string> Preparar(DadosCapturaFoto d) =>
            DadosCapturaFoto.PrepararAsync(JsonSerializer.Serialize(d), registro, obra.Id, db, default);

        foreach (var invalido in new[] { dados with { RegistroId = Guid.NewGuid() }, dados with { ObraId = Guid.NewGuid() },
            dados with { Latitude = null }, dados with { Longitude = 181 }, dados with { PrecisaoMetros = 101 },
            dados with { CapturadaEm = null }, dados with { LocalizacaoObtidaEm = hora.AddSeconds(-61) },
            dados with { Origem = "arquivo" } })
            await Assert.ThrowsAsync<InvalidOperationException>(() => Preparar(invalido));

        var salvo = DadosCapturaFoto.Ler(await Preparar(dados))!;
        Assert.Equal(hora, salvo.CapturadaEm);
        Assert.Equal(registro, salvo.RegistroId);
        Assert.Equal(obra.Nome, salvo.ObraNome);
        Assert.True(salvo.RecebidaEm > hora);
    }

    private static SstDbContext CriarBanco() => new(new DbContextOptionsBuilder<SstDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new CurrentUserService());

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
