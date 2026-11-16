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

    [Fact]
    public async Task Listar_FiltraPorTipoEDevolveOsCamposNovos()
    {
        var (db, obra, camera, recente, antiga) = await Preparar(nameof(Listar_FiltraPorTipoEDevolveOsCamposNovos));
        var funcao = new Funcao { Nome = "Pedreiro", CorCapacete = CorCapacete.Amarelo };
        var joao = new Trabalhador { ObraId = obra.Id, Nome = "João da Silva", FuncaoId = funcao.Id };
        var semOculos = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x03 },
            RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-1), CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-1), PessoasNaCena = 1, SemOculos = 1,
        };
        var cor = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x04 },
            RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-2), CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-2), PessoasNaCena = 1,
            AvisoCorCapacete = true, CorCapaceteVista = CorCapacete.Azul, CorCapaceteEsperada = CorCapacete.Amarelo, TrabalhadorId = joao.Id,
        };
        // Irregular (sem colete) com cor vista diferente da esperada: também entra em tipo=cor. Cor igual ou sem esperada, não.
        var irregularCorDiferente = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x05 },
            RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-3), CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-3), PessoasNaCena = 1, SemColete = 1,
            CorCapaceteVista = CorCapacete.Verde, CorCapaceteEsperada = CorCapacete.Amarelo,
        };
        var irregularCorIgual = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x06 },
            RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-4), CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-4), PessoasNaCena = 1, SemColete = 1,
            CorCapaceteVista = CorCapacete.Amarelo, CorCapaceteEsperada = CorCapacete.Amarelo,
        };
        var irregularSoVista = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8, 0x07 },
            RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-5), CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-5), PessoasNaCena = 1, SemColete = 1,
            CorCapaceteVista = CorCapacete.Verde,
        };
        db.AddRange(funcao, joao, semOculos, cor, irregularCorDiferente, irregularCorIgual, irregularSoVista);
        await db.SaveChangesAsync();
        var handler = new ListarOcorrenciasEpiQueryHandler(db);

        var porOculos = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null, TipoOcorrenciaEpi.Oculos), default);
        Assert.Equal(semOculos.Id, Assert.Single(porOculos).Id);
        Assert.Equal(1, porOculos[0].SemOculos);

        var tiposCor = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null, TipoOcorrenciaEpi.Cor), default);
        Assert.Equal(new[] { cor.Id, irregularCorDiferente.Id }, tiposCor.Select(o => o.Id).ToArray());
        var porCor = tiposCor.Single(o => o.Id == cor.Id);
        Assert.True(porCor.AvisoCorCapacete);
        Assert.Equal(CorCapacete.Azul, porCor.CorCapaceteVista);
        Assert.Equal(CorCapacete.Amarelo, porCor.CorCapaceteEsperada);
        Assert.Equal("João da Silva", porCor.TrabalhadorNome);
        Assert.Equal("Pedreiro", porCor.FuncaoNome);

        var porCapacete = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null, TipoOcorrenciaEpi.Capacete), default);
        Assert.Equal(new[] { recente.Id }, porCapacete.Select(o => o.Id).ToArray());

        var porColete = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null, TipoOcorrenciaEpi.Colete), default);
        Assert.Equal(new[] { irregularCorDiferente.Id, irregularCorIgual.Id, irregularSoVista.Id, antiga.Id }, porColete.Select(o => o.Id).ToArray());

        var todas = await handler.Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null), default);
        Assert.Equal(7, todas.Count);
    }
}
