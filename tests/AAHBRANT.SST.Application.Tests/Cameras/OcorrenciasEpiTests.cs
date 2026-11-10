using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class OcorrenciasEpiTests
{
    private const string Oid = "oid-tecnico-cameras";

    private static async Task<(SstDbContext db, Obra obra, Camera camera, OcorrenciaEpiCamera recente, OcorrenciaEpiCamera antiga)> Preparar(string nomeBanco)
    {
        var db = CamerasPersistenciaTests.CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var recente = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x01 },
            RecebidaEmUtc = DateTime.UtcNow, CapturadaEmUtc = DateTime.UtcNow, PessoasNaCena = 2, SemCapacete = 1,
        };
        var antiga = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x02 },
            RecebidaEmUtc = DateTime.UtcNow.AddDays(-5), CapturadaEmUtc = DateTime.UtcNow.AddDays(-5), PessoasNaCena = 1, SemColete = 1,
            Situacao = SituacaoOcorrenciaEpi.FalsoAlerta,
        };
        db.AddRange(obra, camera, recente, antiga,
            new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico", AzureAdObjectId = Oid });
        await db.SaveChangesAsync();
        return (db, obra, camera, recente, antiga);
    }

    [Fact]
    public async Task Listar_OrdenaDaMaisRecenteEAplicaFiltros()
    {
        var (db, obra, camera, recente, antiga) = await Preparar(nameof(Listar_OrdenaDaMaisRecenteEAplicaFiltros));
        var handler = new ListarOcorrenciasEpiQueryHandler(db);

        var todas = await handler.Handle(new ListarOcorrenciasEpiQuery(obra.Id, camera.Id, null, null, null), default);
        Assert.Equal(new[] { recente.Id, antiga.Id }, todas.Select(o => o.Id).ToArray());
        Assert.Equal("Betoneira", todas[0].CameraNome);
        Assert.Equal("Beira Rio", todas[0].ObraNome);

        var doPeriodo = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, DateTime.UtcNow.AddDays(-1), null, null), default);
        Assert.Equal(recente.Id, Assert.Single(doPeriodo).Id);

        var falsos = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, SituacaoOcorrenciaEpi.FalsoAlerta), default);
        Assert.Equal(antiga.Id, Assert.Single(falsos).Id);
    }

    [Fact]
    public async Task Listar_MantemOcorrenciaMesmoComCameraExcluida()
    {
        var (db, obra, camera, recente, _) = await Preparar(nameof(Listar_MantemOcorrenciaMesmoComCameraExcluida));
        camera.Ativo = false;
        await db.SaveChangesAsync();

        var lista = await new ListarOcorrenciasEpiQueryHandler(db).Handle(
            new ListarOcorrenciasEpiQuery(obra.Id, null, null, null, null), default);

        Assert.Contains(lista, o => o.Id == recente.Id && o.CameraNome == "Betoneira");
    }

    [Fact]
    public async Task ObterFoto_DevolveOsBytesGravados()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(ObterFoto_DevolveOsBytesGravados));

        var foto = await new ObterFotoOcorrenciaEpiQueryHandler(db).Handle(new ObterFotoOcorrenciaEpiQuery(recente.Id), default);

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0x01 }, foto);
    }

    [Fact]
    public async Task ObterFoto_Inexistente_LancaKeyNotFoundException()
    {
        var (db, _, _, _, _) = await Preparar(nameof(ObterFoto_Inexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ObterFotoOcorrenciaEpiQueryHandler(db).Handle(new ObterFotoOcorrenciaEpiQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Vincular_GravaTrabalhadorQuemVinculouEMarcaConferida()
    {
        var (db, obra, _, recente, _) = await Preparar(nameof(Vincular_GravaTrabalhadorQuemVinculouEMarcaConferida));
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "João da Silva" };
        db.Add(trabalhador);
        await db.SaveChangesAsync();

        await new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, trabalhador.Id, Oid), default);

        var lida = await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id);
        Assert.Equal(trabalhador.Id, lida.TrabalhadorId);
        Assert.NotNull(lida.VinculadoEmUtc);
        Assert.Equal((await db.Usuarios.SingleAsync()).Id, lida.VinculadoPorUsuarioId);
        Assert.Equal(SituacaoOcorrenciaEpi.Conferida, lida.Situacao);
    }

    [Fact]
    public async Task Vincular_ComTrabalhadorNulo_RemoveOVinculo()
    {
        var (db, obra, _, recente, _) = await Preparar(nameof(Vincular_ComTrabalhadorNulo_RemoveOVinculo));
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "João da Silva" };
        db.Add(trabalhador);
        recente.TrabalhadorId = trabalhador.Id;
        recente.VinculadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, null, Oid), default);

        var lida = await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id);
        Assert.Null(lida.TrabalhadorId);
        Assert.Null(lida.VinculadoEmUtc);
        Assert.Null(lida.VinculadoPorUsuarioId);
    }

    [Fact]
    public async Task Vincular_TrabalhadorInexistente_LancaKeyNotFoundException()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(Vincular_TrabalhadorInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(recente.Id, Guid.NewGuid(), Oid), default));
    }

    [Fact]
    public async Task DefinirSituacao_MarcaFalsoAlerta()
    {
        var (db, _, _, recente, _) = await Preparar(nameof(DefinirSituacao_MarcaFalsoAlerta));

        await new DefinirSituacaoOcorrenciaEpiCommandHandler(db).Handle(
            new DefinirSituacaoOcorrenciaEpiCommand(recente.Id, SituacaoOcorrenciaEpi.FalsoAlerta), default);

        Assert.Equal(SituacaoOcorrenciaEpi.FalsoAlerta, (await db.OcorrenciasEpiCamera.SingleAsync(o => o.Id == recente.Id)).Situacao);
    }
}
