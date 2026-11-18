using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Fase 2 das câmeras: campos de regras de alerta no cadastro (confirmação, não repetir, modo sombra) e filtro "registro" da aba Alertas.
// A regra de confirmação em si é testada em AnalisarQuadroCameraTests (seção "Fase 2").
public class RegrasAlertaCameraTests
{
    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Obra Obra)> Preparar(string nome)
    {
        var db = CamerasPersistenciaTests.CriarDb(nome);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Add(obra);
        await db.SaveChangesAsync();
        return (db, obra);
    }

    [Fact]
    public async Task CameraNova_NasceEmModoSombraComOsTemposPadrao()
    {
        var (db, obra) = await Preparar(nameof(CameraNova_NasceEmModoSombraComOsTemposPadrao));

        var id = await new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "Betoneira", null, TipoCamera.Canteiro, true, 30), default);

        var camera = await db.Cameras.SingleAsync(c => c.Id == id);
        Assert.True(camera.ModoSombra);
        Assert.Equal(3, camera.PersistenciaCapaceteSegundos);
        Assert.Equal(3, camera.PersistenciaColeteSegundos);
        Assert.Equal(3, camera.PersistenciaOculosSegundos);
        Assert.Equal(300, camera.NaoRepetirSegundos);
        // O Telegram não é mexido: a escolha dele continua a de sempre (ligado) e só fica em pausa enquanto durar a sombra.
        Assert.True(camera.AvisarTelegram);
    }

    [Fact]
    public async Task CameraNova_ComValoresEscolhidosNaTela_GravaExatamenteOQueVeio()
    {
        var (db, obra) = await Preparar(nameof(CameraNova_ComValoresEscolhidosNaTela_GravaExatamenteOQueVeio));

        var id = await new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "Betoneira", null, TipoCamera.Canteiro, true, 30,
                PersistenciaCapaceteSegundos: 5, PersistenciaColeteSegundos: 8, PersistenciaOculosSegundos: 12,
                NaoRepetirSegundos: 0, ModoSombra: false), default);

        var camera = await db.Cameras.SingleAsync(c => c.Id == id);
        Assert.False(camera.ModoSombra);
        Assert.Equal((5, 8, 12), (camera.PersistenciaCapaceteSegundos, camera.PersistenciaColeteSegundos, camera.PersistenciaOculosSegundos));
        Assert.Equal(0, camera.NaoRepetirSegundos);
    }

    [Fact]
    public async Task CameraExistente_ContinuaSemModoSombraEComOsTemposPadrao()
    {
        // É o que a migration grava nas linhas antigas (ModoSombra 0, 3, 3, 3, 300) e o que o modelo assume para quem não informa.
        var (db, obra) = await Preparar(nameof(CameraExistente_ContinuaSemModoSombraEComOsTemposPadrao));
        db.Add(new Camera { ObraId = obra.Id, Nome = "Antiga" });
        await db.SaveChangesAsync();

        var lista = await new ListarCamerasQueryHandler(db).Handle(new ListarCamerasQuery(obra.Id), default);

        var item = Assert.Single(lista);
        Assert.False(item.ModoSombra);
        Assert.True(item.AvisarTelegram);
        Assert.Equal((3, 3, 3, 300), (item.PersistenciaCapaceteSegundos, item.PersistenciaColeteSegundos, item.PersistenciaOculosSegundos, item.NaoRepetirSegundos));
    }

    [Fact]
    public async Task Atualizar_SemOsCamposNovos_NaoMudaOQueJaFoiEscolhido()
    {
        var (db, obra) = await Preparar(nameof(Atualizar_SemOsCamposNovos_NaoMudaOQueJaFoiEscolhido));
        var camera = new Camera
        {
            ObraId = obra.Id, Nome = "Betoneira", ModoSombra = true, AvisarTelegram = false,
            PersistenciaCapaceteSegundos = 7, PersistenciaColeteSegundos = 8, PersistenciaOculosSegundos = 9, NaoRepetirSegundos = 120,
        };
        db.Add(camera);
        await db.SaveChangesAsync();

        // Tela antiga: nenhum dos campos novos no corpo.
        await new AtualizarCameraCommandHandler(db).Handle(
            new AtualizarCameraCommand(camera.Id, "Betoneira 2", null, TipoCamera.Canteiro, true, 30), default);

        var lida = await db.Cameras.SingleAsync();
        Assert.Equal("Betoneira 2", lida.Nome);
        Assert.True(lida.ModoSombra);
        Assert.False(lida.AvisarTelegram);
        Assert.Equal((7, 8, 9, 120), (lida.PersistenciaCapaceteSegundos, lida.PersistenciaColeteSegundos, lida.PersistenciaOculosSegundos, lida.NaoRepetirSegundos));
    }

    [Fact]
    public async Task Atualizar_ComOsCamposNovos_AlteraEDesligarOModoSombraPreservaOTelegram()
    {
        var (db, obra) = await Preparar(nameof(Atualizar_ComOsCamposNovos_AlteraEDesligarOModoSombraPreservaOTelegram));
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira", ModoSombra = true, AvisarTelegram = true };
        db.Add(camera);
        await db.SaveChangesAsync();

        await new AtualizarCameraCommandHandler(db).Handle(
            new AtualizarCameraCommand(camera.Id, "Betoneira", null, TipoCamera.Canteiro, true, 30,
                PersistenciaCapaceteSegundos: 4, PersistenciaColeteSegundos: 6, PersistenciaOculosSegundos: 10,
                NaoRepetirSegundos: 600, ModoSombra: false), default);

        var lida = await db.Cameras.SingleAsync();
        Assert.False(lida.ModoSombra);
        Assert.True(lida.AvisarTelegram);
        Assert.Equal((4, 6, 10, 600), (lida.PersistenciaCapaceteSegundos, lida.PersistenciaColeteSegundos, lida.PersistenciaOculosSegundos, lida.NaoRepetirSegundos));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Validador_PersistenciaVaiDe1a30Segundos(int segundos, bool valido)
    {
        var criar = new CriarCameraCommand(Guid.NewGuid(), "A", null, TipoCamera.Canteiro, true, 30,
            PersistenciaCapaceteSegundos: segundos, PersistenciaColeteSegundos: segundos, PersistenciaOculosSegundos: segundos);
        var atualizar = new AtualizarCameraCommand(Guid.NewGuid(), "A", null, TipoCamera.Canteiro, true, 30,
            PersistenciaCapaceteSegundos: segundos);

        Assert.Equal(valido, new CriarCameraCommandValidator().Validate(criar).IsValid);
        Assert.Equal(valido, new AtualizarCameraCommandValidator().Validate(atualizar).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(3600, true)]
    [InlineData(3601, false)]
    public void Validador_NaoRepetirVaiDe0a3600Segundos(int segundos, bool valido)
    {
        var criar = new CriarCameraCommand(Guid.NewGuid(), "A", null, TipoCamera.Canteiro, true, 30, NaoRepetirSegundos: segundos);
        var atualizar = new AtualizarCameraCommand(Guid.NewGuid(), "A", null, TipoCamera.Canteiro, true, 30, NaoRepetirSegundos: segundos);

        Assert.Equal(valido, new CriarCameraCommandValidator().Validate(criar).IsValid);
        Assert.Equal(valido, new AtualizarCameraCommandValidator().Validate(atualizar).IsValid);
    }

    [Fact]
    public async Task ListarCameras_DevolveOsCamposDeRegrasEContaSoOcorrenciasConfirmadasHoje()
    {
        var (db, obra) = await Preparar(nameof(ListarCameras_DevolveOsCamposDeRegrasEContaSoOcorrenciasConfirmadasHoje));
        var hoje = DateTime.UtcNow.AddHours(-3).Date;
        var camera = new Camera
        {
            ObraId = obra.Id, Nome = "Betoneira", ModoSombra = true, PersistenciaOculosSegundos = 11, NaoRepetirSegundos = 45,
            DiaDoConsumo = hoje, NaoAvaliaveisNoDia = 6,
        };
        db.AddRange(camera,
            Ocorrencia(camera, obra, confirmada: true),
            Ocorrencia(camera, obra, confirmada: false),
            Ocorrencia(camera, obra, confirmada: false));
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ListarCamerasQueryHandler(db).Handle(new ListarCamerasQuery(obra.Id), default));

        Assert.True(item.ModoSombra);
        Assert.Equal(11, item.PersistenciaOculosSegundos);
        Assert.Equal(45, item.NaoRepetirSegundos);
        Assert.Equal(6, item.NaoAvaliaveisNoDia);
        Assert.Equal(1, item.OcorrenciasHoje);
    }

    [Fact]
    public async Task ListarCameras_ContadorDeNaoAvaliaveisDeOutroDia_VemZerado()
    {
        var (db, obra) = await Preparar(nameof(ListarCameras_ContadorDeNaoAvaliaveisDeOutroDia_VemZerado));
        db.Add(new Camera { ObraId = obra.Id, Nome = "Betoneira", DiaDoConsumo = DateTime.UtcNow.AddDays(-2).Date, NaoAvaliaveisNoDia = 9 });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ListarCamerasQueryHandler(db).Handle(new ListarCamerasQuery(obra.Id), default));

        Assert.Equal(0, item.NaoAvaliaveisNoDia);
    }

    private static OcorrenciaEpiCamera Ocorrencia(Camera camera, Obra obra, bool confirmada = true, bool sombra = false, int? segundos = null)
        => new()
        {
            CameraId = camera.Id, ObraId = obra.Id, Foto = new byte[] { 0xFF, 0xD8 }, SemCapacete = 1, PessoasNaCena = 1,
            CapturadaEmUtc = DateTime.UtcNow, RecebidaEmUtc = DateTime.UtcNow,
            Confirmada = confirmada, Sombra = sombra, SegundosObservados = segundos,
        };

    [Fact]
    public async Task ListarOcorrencias_FiltroRegistro_PadraoSombraAvisoENaoConfirmadas()
    {
        var (db, obra) = await Preparar(nameof(ListarOcorrencias_FiltroRegistro_PadraoSombraAvisoENaoConfirmadas));
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var avisoConfirmada = Ocorrencia(camera, obra, confirmada: true, sombra: false, segundos: 5);
        var avisoNaoConfirmada = Ocorrencia(camera, obra, confirmada: false, sombra: false, segundos: 1);
        var sombraConfirmada = Ocorrencia(camera, obra, confirmada: true, sombra: true, segundos: 6);
        var sombraNaoConfirmada = Ocorrencia(camera, obra, confirmada: false, sombra: true, segundos: 2);
        var antiga = Ocorrencia(camera, obra);   // anterior à Fase 2: confirmada, sem segundos observados
        db.AddRange(camera, avisoConfirmada, avisoNaoConfirmada, sombraConfirmada, sombraNaoConfirmada, antiga);
        await db.SaveChangesAsync();
        var handler = new ListarOcorrenciasEpiQueryHandler(db);

        async Task<IEnumerable<Guid>> Ids(RegistroOcorrenciaEpi? registro)
            => (await handler.Handle(new ListarOcorrenciasEpiQuery(obra.Id, null, null, null, null, Registro: registro), default))
                .Select(o => o.Id).Order().ToList();

        // Padrão: some a não confirmada de câmera que avisa; em modo sombra a não confirmada aparece (conferência).
        var esperadoPadrao = new[] { avisoConfirmada.Id, sombraConfirmada.Id, sombraNaoConfirmada.Id, antiga.Id }.Order().ToList();
        Assert.Equal(esperadoPadrao, await Ids(null));
        Assert.Equal(esperadoPadrao, await Ids(RegistroOcorrenciaEpi.Todos));
        Assert.Equal(new[] { sombraConfirmada.Id, sombraNaoConfirmada.Id }.Order().ToList(), await Ids(RegistroOcorrenciaEpi.Sombra));
        // "Só com aviso": câmera avisando e confirmada (a antiga entra: vale como confirmada).
        Assert.Equal(new[] { avisoConfirmada.Id, antiga.Id }.Order().ToList(), await Ids(RegistroOcorrenciaEpi.Aviso));
        Assert.Equal(new[] { avisoNaoConfirmada.Id, sombraNaoConfirmada.Id }.Order().ToList(), await Ids(RegistroOcorrenciaEpi.NaoConfirmadas));
    }

    [Fact]
    public async Task ListarOcorrencias_DevolveConfirmadaSombraESegundosObservados()
    {
        var (db, obra) = await Preparar(nameof(ListarOcorrencias_DevolveConfirmadaSombraESegundosObservados));
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        var nao = Ocorrencia(camera, obra, confirmada: false, sombra: true, segundos: 1);
        var antiga = Ocorrencia(camera, obra);
        db.AddRange(camera, nao, antiga);
        await db.SaveChangesAsync();

        var lista = await new ListarOcorrenciasEpiQueryHandler(db).Handle(new ListarOcorrenciasEpiQuery(obra.Id, null, null, null, null), default);

        var dtoNao = lista.Single(o => o.Id == nao.Id);
        Assert.False(dtoNao.Confirmada);
        Assert.True(dtoNao.Sombra);
        Assert.Equal(1, dtoNao.SegundosObservados);
        var dtoAntiga = lista.Single(o => o.Id == antiga.Id);
        Assert.True(dtoAntiga.Confirmada);
        Assert.False(dtoAntiga.Sombra);
        Assert.Null(dtoAntiga.SegundosObservados);
    }
}
