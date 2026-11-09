using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class CamerasCadastroTests
{
    [Fact]
    public async Task Criar_GravaCameraNaObra()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_GravaCameraNaObra));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Add(obra);
        await db.SaveChangesAsync();

        var id = await new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "  Betoneira ", null, TipoCamera.Canteiro, true, 0), default);

        var camera = await db.Cameras.SingleAsync(c => c.Id == id);
        Assert.Equal("Betoneira", camera.Nome);
        Assert.Equal(obra.Id, camera.ObraId);
    }

    [Fact]
    public async Task Criar_NomeRepetidoNaMesmaObra_LancaInvalidOperationException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_NomeRepetidoNaMesmaObra_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.AddRange(obra, new Camera { ObraId = obra.Id, Nome = "Betoneira" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "Betoneira", null, TipoCamera.Canteiro, true, 0), default));
    }

    [Fact]
    public async Task Criar_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_ObraInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, 0), default));
    }

    [Fact]
    public async Task Atualizar_AlteraCamposSemMudarObra()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Atualizar_AlteraCamposSemMudarObra));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        db.AddRange(obra, camera);
        await db.SaveChangesAsync();

        await new AtualizarCameraCommandHandler(db).Handle(
            new AtualizarCameraCommand(camera.Id, "Portaria", null, TipoCamera.Portaria, false, 10), default);

        var lida = await db.Cameras.SingleAsync();
        Assert.Equal("Portaria", lida.Nome);
        Assert.Equal(TipoCamera.Portaria, lida.Tipo);
        Assert.False(lida.Habilitada);
        Assert.Equal(10, lida.IntervaloMinimoAlertasMinutos);
        Assert.Equal(obra.Id, lida.ObraId);
    }

    [Fact]
    public async Task Excluir_FazExclusaoLogicaEPreservaOcorrencias()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Excluir_FazExclusaoLogicaEPreservaOcorrencias));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var ocorrencia = new OcorrenciaEpiCamera { CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 1 }, SemCapacete = 1, PessoasNaCena = 1 };
        db.AddRange(obra, camera, ocorrencia);
        await db.SaveChangesAsync();

        await new ExcluirCameraCommandHandler(db).Handle(new ExcluirCameraCommand(camera.Id), default);

        Assert.Empty(await db.Cameras.ToListAsync());
        Assert.Single(await db.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task Listar_FiltraPorObraEContaOcorrenciasDeHoje()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Listar_FiltraPorObraEContaOcorrenciasDeHoje));
        var obra1 = new Obra { Codigo = "OB1", Nome = "Obra Um" };
        var obra2 = new Obra { Codigo = "OB2", Nome = "Obra Dois" };
        var camera1 = new Camera { ObraId = obra1.Id, Nome = "Betoneira" };
        var camera2 = new Camera { ObraId = obra2.Id, Nome = "Portaria" };
        db.AddRange(obra1, obra2, camera1, camera2,
            new OcorrenciaEpiCamera { CameraId = camera1.Id, ObraId = obra1.Id, Foto = new byte[] { 1 }, RecebidaEmUtc = DateTime.UtcNow },
            new OcorrenciaEpiCamera { CameraId = camera1.Id, ObraId = obra1.Id, Foto = new byte[] { 1 }, RecebidaEmUtc = DateTime.UtcNow.AddDays(-3) });
        await db.SaveChangesAsync();

        var lista = await new ListarCamerasQueryHandler(db).Handle(new ListarCamerasQuery(obra1.Id), default);

        var item = Assert.Single(lista);
        Assert.Equal("Betoneira", item.Nome);
        Assert.Equal("Obra Um", item.ObraNome);
        Assert.Equal(1, item.OcorrenciasHoje);
    }

    [Fact]
    public void Validador_RecusaNomeVazioEIntervaloNegativo()
    {
        var validador = new CriarCameraCommandValidator();

        Assert.False(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "", null, TipoCamera.Canteiro, true, 0)).IsValid);
        Assert.False(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, -1)).IsValid);
        Assert.True(validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "Betoneira", null, TipoCamera.Canteiro, true, 0)).IsValid);
    }
}
