using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// "Parte da imagem" (câmera de duas lentes empilhadas) e "Avisar no Telegram" por câmera.
public class CamerasParteImagemTests
{
    private sealed record Cenario(SstDbContext Db, Obra Obra, EquipamentoCamera Equipamento);

    private static async Task<Cenario> Preparar(string nomeBanco)
    {
        var db = CamerasPersistenciaTests.CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var agente = new AgenteCamera { ObraId = obra.Id, Nome = "PC", SegredoHash = "hash" };
        var equipamento = new EquipamentoCamera
        {
            ObraId = obra.Id, AgenteCameraId = agente.Id, Nome = "A31", Host = "192.168.1.50", SenhaCifrada = "cifrada",
        };
        db.AddRange(obra, agente, equipamento);
        await db.SaveChangesAsync();
        return new Cenario(db, obra, equipamento);
    }

    private static CriarCameraCommand Ip(Cenario c, string nome, int canal = 1, ParteImagemCamera parte = ParteImagemCamera.Inteira, bool avisar = true)
        => new(c.Obra.Id, nome, null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, c.Equipamento.Id, canal, 1, 3, parte, avisar);

    private static Task<Guid> Criar(Cenario c, CriarCameraCommand cmd) => new CriarCameraCommandHandler(c.Db).Handle(cmd, default);

    // ---------- Padrões ----------

    [Fact]
    public async Task CameraNova_NasceInteiraEComAvisoNoTelegram()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(CameraNova_NasceInteiraEComAvisoNoTelegram));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.AddRange(obra, new Camera { ObraId = obra.Id, Nome = "Betoneira" });
        await db.SaveChangesAsync();

        var camera = await db.Cameras.AsNoTracking().SingleAsync();
        Assert.Equal(ParteImagemCamera.Inteira, camera.ParteImagem);
        Assert.True(camera.AvisarTelegram);

        // Tela antiga (sem os campos novos): o comando assume imagem inteira e com aviso.
        var comando = new CriarCameraCommand(obra.Id, "Portaria", null, TipoCamera.Portaria, true, 0);
        Assert.Equal(ParteImagemCamera.Inteira, comando.ParteImagem);
        Assert.True(comando.AvisarTelegram);
        var id = await new CriarCameraCommandHandler(db).Handle(comando, default);
        var criada = await db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(ParteImagemCamera.Inteira, criada.ParteImagem);
        Assert.True(criada.AvisarTelegram);
    }

    [Fact]
    public void Configuracao_DeixaOsPadroesNoBancoParaLinhasJaGravadas_ESemOmitirFalseNoInsert()
    {
        using var db = CamerasPersistenciaTests.CriarDb(nameof(Configuracao_DeixaOsPadroesNoBancoParaLinhasJaGravadas_ESemOmitirFalseNoInsert));
        var cameras = db.Model.FindEntityType(typeof(Camera))!;

        Assert.Equal(ParteImagemCamera.Inteira, cameras.FindProperty(nameof(Camera.ParteImagem))!.GetDefaultValue());
        var avisar = cameras.FindProperty(nameof(Camera.AvisarTelegram))!;
        Assert.Equal(true, avisar.GetDefaultValue());
        // Com o default true e geração "ao adicionar", o EF omitiria o false do INSERT e o banco gravaria true.
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never, avisar.ValueGenerated);

        var testes = db.Model.FindEntityType(typeof(TesteConexaoCamera))!;
        Assert.Equal(ParteImagemCamera.Inteira, testes.FindProperty(nameof(TesteConexaoCamera.ParteImagem))!.GetDefaultValue());
    }

    // ---------- Criar ----------

    [Fact]
    public async Task Criar_GravaParteEAvisoInformados()
    {
        var c = await Preparar(nameof(Criar_GravaParteEAvisoInformados));

        var id = await Criar(c, Ip(c, "A31 cima", parte: ParteImagemCamera.MetadeSuperior, avisar: false));

        var camera = await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(ParteImagemCamera.MetadeSuperior, camera.ParteImagem);
        Assert.False(camera.AvisarTelegram);
    }

    [Fact]
    public async Task Criar_AvisoDesligadoEmCameraDoNavegador_Vale_ParteInteiraSempre()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_AvisoDesligadoEmCameraDoNavegador_Vale_ParteInteiraSempre));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Add(obra);
        await db.SaveChangesAsync();

        var id = await new CriarCameraCommandHandler(db).Handle(
            new CriarCameraCommand(obra.Id, "Tablet", null, TipoCamera.Canteiro, true, 0, AvisarTelegram: false), default);

        var camera = await db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(camera.AvisarTelegram);
        Assert.Equal(ParteImagemCamera.Inteira, camera.ParteImagem);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task Validador_Criar_RecusaParteForaDeZeroADois(int parte)
    {
        var c = await Preparar(nameof(Validador_Criar_RecusaParteForaDeZeroADois) + parte);
        var validador = new CriarCameraCommandValidator();

        var resultado = validador.Validate(Ip(c, "A31", parte: (ParteImagemCamera)parte));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarCameraCommand.ParteImagem));
    }

    [Theory]
    [InlineData(ParteImagemCamera.MetadeSuperior)]
    [InlineData(ParteImagemCamera.MetadeInferior)]
    public void Validador_Criar_RecusaParteDiferenteDeInteiraEmCameraQueNaoEIp(ParteImagemCamera parte)
    {
        var validador = new CriarCameraCommandValidator();

        var resultado = validador.Validate(new CriarCameraCommand(Guid.NewGuid(), "Tablet", null, TipoCamera.Canteiro, true, 0, ParteImagem: parte));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarCameraCommand.ParteImagem));
    }

    [Fact]
    public async Task Validador_Criar_AceitaTodasAsPartesEmCameraIp()
    {
        var c = await Preparar(nameof(Validador_Criar_AceitaTodasAsPartesEmCameraIp));
        var validador = new CriarCameraCommandValidator();

        foreach (var parte in Enum.GetValues<ParteImagemCamera>())
            Assert.True(validador.Validate(Ip(c, "A31", parte: parte)).IsValid, parte.ToString());
    }

    // ---------- Unicidade por equipamento + canal + parte ----------

    [Fact]
    public async Task DuasCamerasNoMesmoEquipamentoELente_ComPartesDiferentes_SaoAceitas()
    {
        var c = await Preparar(nameof(DuasCamerasNoMesmoEquipamentoELente_ComPartesDiferentes_SaoAceitas));

        await Criar(c, Ip(c, "A31 cima", canal: 1, parte: ParteImagemCamera.MetadeSuperior));
        await Criar(c, Ip(c, "A31 baixo", canal: 1, parte: ParteImagemCamera.MetadeInferior, avisar: false));
        await Criar(c, Ip(c, "A31 inteira", canal: 1, parte: ParteImagemCamera.Inteira));

        Assert.Equal(3, await c.Db.Cameras.CountAsync(x => x.EquipamentoCameraId == c.Equipamento.Id && x.Canal == 1));
    }

    [Theory]
    [InlineData(ParteImagemCamera.Inteira)]
    [InlineData(ParteImagemCamera.MetadeSuperior)]
    [InlineData(ParteImagemCamera.MetadeInferior)]
    public async Task MesmaParteNaMesmaLente_SegueRecusada(ParteImagemCamera parte)
    {
        var c = await Preparar(nameof(MesmaParteNaMesmaLente_SegueRecusada) + parte);
        await Criar(c, Ip(c, "Primeira", canal: 1, parte: parte));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(c, Ip(c, "Segunda", canal: 1, parte: parte)));

        Assert.Contains("canal 1", erro.Message);
    }

    [Fact]
    public async Task LenteDiferente_ComAMesmaParte_ContinuaAceita()
    {
        var c = await Preparar(nameof(LenteDiferente_ComAMesmaParte_ContinuaAceita));
        await Criar(c, Ip(c, "Lente 1 cima", canal: 1, parte: ParteImagemCamera.MetadeSuperior));

        await Criar(c, Ip(c, "Lente 2 cima", canal: 2, parte: ParteImagemCamera.MetadeSuperior));

        Assert.Equal(2, await c.Db.Cameras.CountAsync());
    }

    // ---------- Atualizar ----------

    private static async Task<Guid> CriarIpDireto(Cenario c, string nome, int canal, ParteImagemCamera parte)
    {
        var camera = new Camera
        {
            ObraId = c.Obra.Id, Nome = nome, Fonte = FonteCamera.Rtsp, EquipamentoCameraId = c.Equipamento.Id, Canal = canal, Stream = 1, ParteImagem = parte,
        };
        c.Db.Add(camera);
        await c.Db.SaveChangesAsync();
        return camera.Id;
    }

    private static Task Atualizar(Cenario c, AtualizarCameraCommand cmd) => new AtualizarCameraCommandHandler(c.Db).Handle(cmd, default);

    [Fact]
    public async Task Atualizar_SemOsCamposNovos_NaoDesfazAEscolhaJaFeita()
    {
        var c = await Preparar(nameof(Atualizar_SemOsCamposNovos_NaoDesfazAEscolhaJaFeita));
        var id = await Criar(c, Ip(c, "A31 baixo", parte: ParteImagemCamera.MetadeInferior, avisar: false));

        // Tela antiga: reenvia a origem da câmera, mas não conhece parteImagem nem avisarTelegram.
        await Atualizar(c, new AtualizarCameraCommand(id, "A31 baixo (renomeada)", null, TipoCamera.Canteiro, true, 30,
            FonteCamera.Rtsp, c.Equipamento.Id, 1, 1, 3));

        var camera = await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(ParteImagemCamera.MetadeInferior, camera.ParteImagem);
        Assert.False(camera.AvisarTelegram);
    }

    [Fact]
    public async Task Atualizar_TrocaParteEAviso_ESoMudarAParteReiniciaASituacaoDaConexao()
    {
        var c = await Preparar(nameof(Atualizar_TrocaParteEAviso_ESoMudarAParteReiniciaASituacaoDaConexao));
        var id = await Criar(c, Ip(c, "A31"));
        var camera = await c.Db.Cameras.SingleAsync(x => x.Id == id);
        camera.SituacaoConexao = SituacaoConexaoCamera.RecebendoImagem;
        camera.UltimoQuadroRecebidoEmUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();

        // Só o aviso muda: a leitura da câmera não é afetada.
        await Atualizar(c, new AtualizarCameraCommand(id, "A31", null, TipoCamera.Canteiro, true, 30, AvisarTelegram: false));
        c.Db.ChangeTracker.Clear();
        var depoisDoAviso = await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(depoisDoAviso.AvisarTelegram);
        Assert.Equal(SituacaoConexaoCamera.RecebendoImagem, depoisDoAviso.SituacaoConexao);

        // Muda a parte: a câmera volta a "desconhecida" até o agente reenviar quadros recortados.
        await Atualizar(c, new AtualizarCameraCommand(id, "A31", null, TipoCamera.Canteiro, true, 30, ParteImagem: ParteImagemCamera.MetadeSuperior));
        c.Db.ChangeTracker.Clear();
        var depoisDaParte = await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(ParteImagemCamera.MetadeSuperior, depoisDaParte.ParteImagem);
        Assert.Equal(SituacaoConexaoCamera.Desconhecida, depoisDaParte.SituacaoConexao);
        Assert.Null(depoisDaParte.UltimoQuadroRecebidoEmUtc);
    }

    [Fact]
    public async Task Atualizar_ParteJaUsadaPorOutraCameraDaMesmaLente_Recusa_ParteLivre_Aceita()
    {
        var c = await Preparar(nameof(Atualizar_ParteJaUsadaPorOutraCameraDaMesmaLente_Recusa_ParteLivre_Aceita));
        await CriarIpDireto(c, "Cima", 1, ParteImagemCamera.MetadeSuperior);
        var baixo = await CriarIpDireto(c, "Baixo", 1, ParteImagemCamera.MetadeInferior);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Atualizar(c,
            new AtualizarCameraCommand(baixo, "Baixo", null, TipoCamera.Canteiro, true, 30, ParteImagem: ParteImagemCamera.MetadeSuperior)));

        // Pela origem completa (Fonte informada) a regra é a mesma.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Atualizar(c,
            new AtualizarCameraCommand(baixo, "Baixo", null, TipoCamera.Canteiro, true, 30,
                FonteCamera.Rtsp, c.Equipamento.Id, 1, 1, 3, ParteImagemCamera.MetadeSuperior)));

        await Atualizar(c, new AtualizarCameraCommand(baixo, "Baixo", null, TipoCamera.Canteiro, true, 30, ParteImagem: ParteImagemCamera.Inteira));
        c.Db.ChangeTracker.Clear();
        Assert.Equal(ParteImagemCamera.Inteira, (await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == baixo)).ParteImagem);
    }

    [Fact]
    public async Task Atualizar_CameraQueNaoEIp_RecusaParteDiferenteDeInteira()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Atualizar_CameraQueNaoEIp_RecusaParteDiferenteDeInteira));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var tablet = new Camera { ObraId = obra.Id, Nome = "Tablet" };
        db.AddRange(obra, tablet);
        await db.SaveChangesAsync();

        // Sem Fonte no pedido: vale a câmera já gravada (navegador) e o handler recusa.
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AtualizarCameraCommandHandler(db).Handle(
            new AtualizarCameraCommand(tablet.Id, "Tablet", null, TipoCamera.Canteiro, true, 30, ParteImagem: ParteImagemCamera.MetadeSuperior), default));

        // Com Fonte = navegador no pedido o validador já recusa.
        var validador = new AtualizarCameraCommandValidator();
        Assert.False(validador.Validate(new AtualizarCameraCommand(tablet.Id, "Tablet", null, TipoCamera.Canteiro, true, 30,
            FonteCamera.Navegador, ParteImagem: ParteImagemCamera.MetadeInferior)).IsValid);
        Assert.False(validador.Validate(new AtualizarCameraCommand(tablet.Id, "Tablet", null, TipoCamera.Canteiro, true, 30,
            ParteImagem: (ParteImagemCamera)3)).IsValid);
        Assert.True(validador.Validate(new AtualizarCameraCommand(tablet.Id, "Tablet", null, TipoCamera.Canteiro, true, 30,
            FonteCamera.Navegador, ParteImagem: ParteImagemCamera.Inteira, AvisarTelegram: false)).IsValid);
    }

    [Fact]
    public async Task Atualizar_VoltarParaNavegador_ZeraAParte()
    {
        var c = await Preparar(nameof(Atualizar_VoltarParaNavegador_ZeraAParte));
        var id = await CriarIpDireto(c, "Cima", 1, ParteImagemCamera.MetadeSuperior);

        await Atualizar(c, new AtualizarCameraCommand(id, "Cima", null, TipoCamera.Canteiro, true, 30, FonteCamera.Navegador));

        c.Db.ChangeTracker.Clear();
        var camera = await c.Db.Cameras.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(FonteCamera.Navegador, camera.Fonte);
        Assert.Equal(ParteImagemCamera.Inteira, camera.ParteImagem);
    }

    // ---------- Listar (alimenta a lista e o Mosaico) ----------

    [Fact]
    public async Task Listar_TrazParteEAviso()
    {
        var c = await Preparar(nameof(Listar_TrazParteEAviso));
        await Criar(c, Ip(c, "A31 cima", parte: ParteImagemCamera.MetadeSuperior));
        await Criar(c, Ip(c, "A31 baixo", parte: ParteImagemCamera.MetadeInferior, avisar: false));

        var lista = await new ListarCamerasQueryHandler(c.Db).Handle(new ListarCamerasQuery(c.Obra.Id), default);

        var cima = lista.Single(x => x.Nome == "A31 cima");
        Assert.Equal(ParteImagemCamera.MetadeSuperior, cima.ParteImagem);
        Assert.True(cima.AvisarTelegram);
        var baixo = lista.Single(x => x.Nome == "A31 baixo");
        Assert.Equal(ParteImagemCamera.MetadeInferior, baixo.ParteImagem);
        Assert.False(baixo.AvisarTelegram);
    }

    // ---------- Teste de conexão ----------

    [Fact]
    public async Task TesteDeConexao_PorCamera_UsaAParteDaCamera()
    {
        var c = await Preparar(nameof(TesteDeConexao_PorCamera_UsaAParteDaCamera));
        var id = await CriarIpDireto(c, "Baixo", 1, ParteImagemCamera.MetadeInferior);

        var testeId = await new SolicitarTesteConexaoCameraCommandHandler(c.Db).Handle(new SolicitarTesteConexaoCameraCommand(CameraId: id), default);

        Assert.Equal(ParteImagemCamera.MetadeInferior, (await c.Db.TestesConexaoCamera.AsNoTracking().SingleAsync(t => t.Id == testeId)).ParteImagem);
    }

    [Fact]
    public async Task TesteDeConexao_PorEquipamento_PropagaAParteInformada_EPedidosDePartesDiferentesNaoSeConfundem()
    {
        var c = await Preparar(nameof(TesteDeConexao_PorEquipamento_PropagaAParteInformada_EPedidosDePartesDiferentesNaoSeConfundem));
        var handler = new SolicitarTesteConexaoCameraCommandHandler(c.Db);

        var cima = await handler.Handle(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.Equipamento.Id, Canal: 1, Stream: 1,
            ParteImagem: ParteImagemCamera.MetadeSuperior), default);
        var cimaDeNovo = await handler.Handle(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.Equipamento.Id, Canal: 1, Stream: 1,
            ParteImagem: ParteImagemCamera.MetadeSuperior), default);
        var baixo = await handler.Handle(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.Equipamento.Id, Canal: 1, Stream: 1,
            ParteImagem: ParteImagemCamera.MetadeInferior), default);
        var semParte = await handler.Handle(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.Equipamento.Id, Canal: 1, Stream: 1), default);

        Assert.Equal(cima, cimaDeNovo); // duplo clique continua reaproveitando o pedido em aberto
        Assert.Equal(3, new[] { cima, baixo, semParte }.Distinct().Count());
        var testes = await c.Db.TestesConexaoCamera.AsNoTracking().ToDictionaryAsync(t => t.Id);
        Assert.Equal(ParteImagemCamera.MetadeSuperior, testes[cima].ParteImagem);
        Assert.Equal(ParteImagemCamera.MetadeInferior, testes[baixo].ParteImagem);
        Assert.Equal(ParteImagemCamera.Inteira, testes[semParte].ParteImagem);
    }

    [Fact]
    public void Validador_TesteDeConexao_RecusaParteInvalida()
    {
        var validador = new SolicitarTesteConexaoCameraCommandValidator();

        Assert.False(validador.Validate(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: Guid.NewGuid(), ParteImagem: (ParteImagemCamera)3)).IsValid);
        Assert.True(validador.Validate(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: Guid.NewGuid(), ParteImagem: ParteImagemCamera.MetadeSuperior)).IsValid);
        Assert.True(validador.Validate(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: Guid.NewGuid())).IsValid);
    }
}
