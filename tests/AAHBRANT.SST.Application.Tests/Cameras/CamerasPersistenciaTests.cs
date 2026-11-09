using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class CamerasPersistenciaTests
{
    internal static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task GravaCameraEOcorrencia_ComValoresPadrao()
    {
        var db = CriarDb(nameof(GravaCameraEOcorrencia_ComValoresPadrao));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira", Tipo = TipoCamera.Canteiro };
        var ocorrencia = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id,
            ObraId = obra.Id,
            CapturadaEmUtc = DateTime.UtcNow,
            RecebidaEmUtc = DateTime.UtcNow,
            Foto = new byte[] { 0xFF, 0xD8, 0x00 },
            PessoasNaCena = 2,
            SemCapacete = 1,
            Confianca = ConfiancaAnaliseEpi.Alta,
        };
        db.AddRange(obra, camera, ocorrencia);
        await db.SaveChangesAsync();

        var lida = await db.Cameras.SingleAsync();
        Assert.True(lida.Habilitada);
        Assert.Equal(0, lida.IntervaloMinimoAlertasMinutos);
        var ocorrenciaLida = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(SituacaoOcorrenciaEpi.Nova, ocorrenciaLida.Situacao);
        Assert.False(ocorrenciaLida.AlertaEnviado);
    }
}
