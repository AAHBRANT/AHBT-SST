using System.Text.Json;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Cadastro de equipamentos de câmera IP e das câmeras com fonte Rtsp: senha cifrada que nunca volta, mesma obra em tudo
// (equipamento, agente e câmera) e câmera IP só ativa com senha definida.
public class EquipamentosCameraTests
{
    private static readonly ICredencialCameraCriptografia Cripto = new CredencialCameraCriptografiaService();

    public EquipamentosCameraTests() => ChaveCredencialCameraDeTeste.Configurar();

    private sealed record Cenario(SstDbContext Db, Obra ObraA, Obra ObraB, AgenteCamera AgenteA, AgenteCamera AgenteB);

    private static async Task<Cenario> Preparar(string nome)
    {
        var db = CamerasPersistenciaTests.CriarDb(nome);
        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var agenteA = new AgenteCamera { ObraId = obraA.Id, Nome = "PC A", SegredoHash = "x" };
        var agenteB = new AgenteCamera { ObraId = obraB.Id, Nome = "PC B", SegredoHash = "x" };
        db.AddRange(obraA, obraB, agenteA, agenteB);
        await db.SaveChangesAsync();
        return new Cenario(db, obraA, obraB, agenteA, agenteB);
    }

    private static CriarEquipamentoCameraCommand Novo(Cenario c, string nome = "A31 portaria", string? senha = "s3nh@-forte", string host = "192.168.1.50")
        => new(c.ObraA.Id, c.AgenteA.Id, nome, host, Senha: senha, Usuario: "admin");

    private static Task<Guid> Criar(Cenario c, CriarEquipamentoCameraCommand cmd)
        => new CriarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(cmd, default);

    // ---------- Equipamento ----------

    [Fact]
    public async Task Criar_GravaSenhaCifrada_ENuncaEmTextoPlano()
    {
        var c = await Preparar(nameof(Criar_GravaSenhaCifrada_ENuncaEmTextoPlano));

        var id = await Criar(c, Novo(c, senha: "s3nh@-forte"));

        var e = await c.Db.EquipamentosCamera.SingleAsync(x => x.Id == id);
        Assert.NotNull(e.SenhaCifrada);
        Assert.DoesNotContain("s3nh@-forte", e.SenhaCifrada);
        Assert.Equal("s3nh@-forte", Cripto.Decifrar(e.SenhaCifrada!));
        Assert.Equal(554, e.PortaRtsp);
        Assert.Equal(8899, e.PortaOnvif);
        Assert.Equal(c.AgenteA.Id, e.AgenteCameraId);
    }

    [Fact]
    public async Task Criar_MesmaSenhaDuasVezes_GeraTextoCifradoDiferente()
    {
        var c = await Preparar(nameof(Criar_MesmaSenhaDuasVezes_GeraTextoCifradoDiferente));

        var a = await Criar(c, Novo(c, "E1"));
        var b = await Criar(c, Novo(c, "E2"));

        var lidas = await c.Db.EquipamentosCamera.Where(e => e.Id == a || e.Id == b).Select(e => e.SenhaCifrada).ToListAsync();
        Assert.NotEqual(lidas[0], lidas[1]);
    }

    [Fact]
    public async Task Criar_SemSenha_FicaSemSenhaDefinida()
    {
        var c = await Preparar(nameof(Criar_SemSenha_FicaSemSenhaDefinida));

        var id = await Criar(c, Novo(c, senha: null));

        Assert.Null((await c.Db.EquipamentosCamera.SingleAsync(e => e.Id == id)).SenhaCifrada);
        var dto = Assert.Single(await new ListarEquipamentosCameraQueryHandler(c.Db).Handle(new ListarEquipamentosCameraQuery(null), default));
        Assert.False(dto.SenhaDefinida);
    }

    [Fact]
    public async Task Criar_AgenteDeOutraObra_Recusa()
    {
        var c = await Preparar(nameof(Criar_AgenteDeOutraObra_Recusa));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Criar(c, Novo(c) with { AgenteCameraId = c.AgenteB.Id }));

        Assert.Contains("outra obra", ex.Message);
        Assert.Empty(c.Db.EquipamentosCamera);
    }

    [Fact]
    public async Task Criar_AgenteRevogado_Recusa()
    {
        var c = await Preparar(nameof(Criar_AgenteRevogado_Recusa));
        c.AgenteA.Ativo = false;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(c, Novo(c)));
    }

    [Fact]
    public async Task Criar_AgenteInexistente_LancaKeyNotFound()
    {
        var c = await Preparar(nameof(Criar_AgenteInexistente_LancaKeyNotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Criar(c, Novo(c) with { AgenteCameraId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Criar_NomeRepetidoNaObra_Recusa_ExcluidoLiberaONome()
    {
        var c = await Preparar(nameof(Criar_NomeRepetidoNaObra_Recusa_ExcluidoLiberaONome));
        var id = await Criar(c, Novo(c, "Portaria"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(c, Novo(c, "Portaria")));

        await new ExcluirEquipamentoCameraCommandHandler(c.Db).Handle(new ExcluirEquipamentoCameraCommand(id), default);
        await Criar(c, Novo(c, "Portaria"));
        Assert.Single(await c.Db.EquipamentosCamera.ToListAsync());
    }

    [Theory]
    [InlineData("rtsp://192.168.1.50")]
    [InlineData("usuario@192.168.1.50")]
    [InlineData("192.168.1.50/stream")]
    [InlineData("192.168.1.50 ")]
    [InlineData("")]
    public void Validador_RecusaHostComEsquemaBarraArrobaOuEspaco(string host)
    {
        var resultado = new CriarEquipamentoCameraCommandValidator().Validate(
            new CriarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", host));

        Assert.False(resultado.IsValid);
    }

    [Theory]
    [InlineData("192.168.1.50")]
    [InlineData("camera-portaria.local")]
    public void Validador_AceitaIpEHostDeRede(string host)
    {
        Assert.True(new CriarEquipamentoCameraCommandValidator().Validate(
            new CriarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", host)).IsValid);
    }

    [Fact]
    public void Validador_RecusaPortaForaDaFaixaEMacInvalido()
    {
        var v = new CriarEquipamentoCameraCommandValidator();
        var baseCmd = new CriarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", "10.0.0.1");

        Assert.False(v.Validate(baseCmd with { PortaRtsp = 0 }).IsValid);
        Assert.False(v.Validate(baseCmd with { PortaOnvif = 70000 }).IsValid);
        Assert.False(v.Validate(baseCmd with { EnderecoMac = "ZZ:11" }).IsValid);
        Assert.False(v.Validate(baseCmd with { Senha = new string('x', 101) }).IsValid);
        Assert.True(v.Validate(baseCmd with { EnderecoMac = "aa-bb-cc-dd-ee-ff" }).IsValid);
    }

    [Fact]
    public async Task Criar_NormalizaMacParaMaiusculasComDoisPontos()
    {
        var c = await Preparar(nameof(Criar_NormalizaMacParaMaiusculasComDoisPontos));

        var id = await Criar(c, Novo(c) with { EnderecoMac = "aa-bb-cc-dd-ee-0f" });

        Assert.Equal("AA:BB:CC:DD:EE:0F", (await c.Db.EquipamentosCamera.SingleAsync(e => e.Id == id)).EnderecoMac);
    }

    [Fact]
    public async Task Atualizar_NaoMexeNaSenha_EValidaAgenteDaMesmaObra()
    {
        var c = await Preparar(nameof(Atualizar_NaoMexeNaSenha_EValidaAgenteDaMesmaObra));
        var id = await Criar(c, Novo(c));
        var antes = (await c.Db.EquipamentosCamera.SingleAsync()).SenhaCifrada;
        var handler = new AtualizarEquipamentoCameraCommandHandler(c.Db, Cripto);

        await handler.Handle(new AtualizarEquipamentoCameraCommand(id, c.AgenteA.Id, "Novo nome", "192.168.1.50", 8554, 80), default);

        var e = await c.Db.EquipamentosCamera.SingleAsync();
        Assert.Equal("Novo nome", e.Nome);
        Assert.Equal("192.168.1.50", e.Host);
        Assert.Equal(8554, e.PortaRtsp);
        Assert.Equal(antes, e.SenhaCifrada);
        Assert.Equal(c.ObraA.Id, e.ObraId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AtualizarEquipamentoCameraCommand(id, c.AgenteB.Id, "X", "10.0.0.1"), default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Atualizar_TrocandoOEndereco_SemSenha_Recusa_ENadaMuda(string? senha)
    {
        var c = await Preparar("AtualizarHostSemSenha_" + Guid.NewGuid());
        var id = await Criar(c, Novo(c, senha: "original"));
        var antes = await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new AtualizarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(
            new AtualizarEquipamentoCameraCommand(id, c.AgenteA.Id, antes.Nome, "10.9.9.9", Senha: senha), default));

        Assert.Equal("Informe a senha novamente ao trocar o endereço da câmera.", ex.Message);
        c.Db.ChangeTracker.Clear();
        var depois = await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync();
        Assert.Equal(antes.Host, depois.Host);
        Assert.Equal(antes.SenhaCifrada, depois.SenhaCifrada);
    }

    [Fact]
    public async Task Atualizar_TrocandoOEndereco_ComSenha_GravaOsDois_SemExpor()
    {
        var c = await Preparar(nameof(Atualizar_TrocandoOEndereco_ComSenha_GravaOsDois_SemExpor));
        var id = await Criar(c, Novo(c, senha: "original"));
        var nome = (await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync()).Nome;

        await new AtualizarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(
            new AtualizarEquipamentoCameraCommand(id, c.AgenteA.Id, nome, " 10.9.9.9 ", Senha: "nova-senha"), default);

        var e = await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync();
        Assert.Equal("10.9.9.9", e.Host);
        Assert.Equal("nova-senha", Cripto.Decifrar(e.SenhaCifrada!));
        Assert.DoesNotContain("nova-senha", new AtualizarEquipamentoCameraCommand(id, c.AgenteA.Id, nome, "10.9.9.9", Senha: "nova-senha").ToString());
    }

    [Fact]
    public async Task Atualizar_MesmoEnderecoEmOutraCaixa_NaoExigeSenha()
    {
        var c = await Preparar(nameof(Atualizar_MesmoEnderecoEmOutraCaixa_NaoExigeSenha));
        var id = await Criar(c, Novo(c, senha: "original"));
        var e0 = await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync();

        await new AtualizarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(
            new AtualizarEquipamentoCameraCommand(id, c.AgenteA.Id, e0.Nome, e0.Host.ToUpperInvariant()), default);

        Assert.Equal(e0.SenhaCifrada, (await c.Db.EquipamentosCamera.AsNoTracking().SingleAsync()).SenhaCifrada);
    }

    [Fact]
    public void Atualizar_Validador_RecusaSenhaGrandeDemais()
    {
        var v = new AtualizarEquipamentoCameraCommandValidator();
        var longa = new string('x', EquipamentoCameraRegras.TamanhoMaximoSenha + 1);

        Assert.False(v.Validate(new AtualizarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", "10.0.0.1", Senha: longa)).IsValid);
        Assert.True(v.Validate(new AtualizarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", "10.0.0.1", Senha: "ok")).IsValid);
    }

    [Fact]
    public async Task DefinirSenha_TrocaACifra_ERecusaVazia()
    {
        var c = await Preparar(nameof(DefinirSenha_TrocaACifra_ERecusaVazia));
        var id = await Criar(c, Novo(c, senha: "antiga"));

        await new DefinirSenhaEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(new DefinirSenhaEquipamentoCameraCommand(id, "nova-senha"), default);

        var e = await c.Db.EquipamentosCamera.SingleAsync();
        Assert.Equal("nova-senha", Cripto.Decifrar(e.SenhaCifrada!));
        Assert.False(new DefinirSenhaEquipamentoCameraCommandValidator().Validate(new DefinirSenhaEquipamentoCameraCommand(id, "")).IsValid);
    }

    [Fact]
    public async Task Excluir_RecusaComCameraEmUso_ELiberaDepois()
    {
        var c = await Preparar(nameof(Excluir_RecusaComCameraEmUso_ELiberaDepois));
        var equipamentoId = await Criar(c, Novo(c));
        var cameraId = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "Lente 1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, equipamentoId, 1, 1), default);
        var excluir = new ExcluirEquipamentoCameraCommandHandler(c.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => excluir.Handle(new ExcluirEquipamentoCameraCommand(equipamentoId), default));

        await new ExcluirCameraCommandHandler(c.Db).Handle(new ExcluirCameraCommand(cameraId), default);
        await excluir.Handle(new ExcluirEquipamentoCameraCommand(equipamentoId), default);
        Assert.False((await c.Db.EquipamentosCamera.IgnoreQueryFilters().SingleAsync()).Ativo);
    }

    [Fact]
    public async Task Listar_NuncaDevolveSenha_NemCifrada()
    {
        var c = await Preparar(nameof(Listar_NuncaDevolveSenha_NemCifrada));
        await Criar(c, Novo(c, senha: "segredo-que-nao-pode-vazar"));
        var cifrada = (await c.Db.EquipamentosCamera.SingleAsync()).SenhaCifrada!;

        var lista = await new ListarEquipamentosCameraQueryHandler(c.Db).Handle(new ListarEquipamentosCameraQuery(null), default);

        var dto = Assert.Single(lista);
        Assert.True(dto.SenhaDefinida);
        var json = JsonSerializer.Serialize(lista);
        Assert.DoesNotContain("segredo-que-nao-pode-vazar", json);
        Assert.DoesNotContain(cifrada, json);
        Assert.DoesNotContain(typeof(EquipamentoCameraDto).GetProperties(), p => p.Name.Contains("Senha") && p.Name != nameof(EquipamentoCameraDto.SenhaDefinida));
        Assert.Equal("Obra A", dto.ObraNome);
        Assert.Equal("PC A", dto.AgenteNome);
    }

    [Fact]
    public async Task Listar_FiltraPorObra_EContaCameras()
    {
        var c = await Preparar(nameof(Listar_FiltraPorObra_EContaCameras));
        var a = await Criar(c, Novo(c, "EA"));
        await new CriarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(
            new CriarEquipamentoCameraCommand(c.ObraB.Id, c.AgenteB.Id, "EB", "10.0.0.2"), default);
        await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, a, 1, 0), default);

        var lista = await new ListarEquipamentosCameraQueryHandler(c.Db).Handle(new ListarEquipamentosCameraQuery(c.ObraA.Id), default);

        var dto = Assert.Single(lista);
        Assert.Equal("EA", dto.Nome);
        Assert.Equal(1, dto.QuantidadeCameras);
    }

    // ---------- Câmera com fonte Rtsp ----------

    [Fact]
    public async Task CriarCamera_SemFonte_ContinuaNavegador_ComPadroes()
    {
        var c = await Preparar(nameof(CriarCamera_SemFonte_ContinuaNavegador_ComPadroes));

        var id = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "Tablet", null, TipoCamera.Canteiro, true, 30), default);

        var cam = await c.Db.Cameras.SingleAsync(x => x.Id == id);
        Assert.Equal(FonteCamera.Navegador, cam.Fonte);
        Assert.Null(cam.EquipamentoCameraId);
        Assert.Null(cam.Canal);
        Assert.Equal(3, cam.IntervaloEnvioSegundos);
        Assert.Equal(SituacaoConexaoCamera.Desconhecida, cam.SituacaoConexao);
    }

    [Fact]
    public async Task CriarCameraRtsp_GravaEquipamentoCanalEStream()
    {
        var c = await Preparar(nameof(CriarCameraRtsp_GravaEquipamentoCanalEStream));
        var eq = await Criar(c, Novo(c));

        var id = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "Lente 2", null, TipoCamera.Portaria, true, 30, FonteCamera.Rtsp, eq, 2, 1, 5), default);

        var cam = await c.Db.Cameras.SingleAsync(x => x.Id == id);
        Assert.Equal(FonteCamera.Rtsp, cam.Fonte);
        Assert.Equal(eq, cam.EquipamentoCameraId);
        Assert.Equal(2, cam.Canal);
        Assert.Equal(1, cam.Stream);
        Assert.Equal(5, cam.IntervaloEnvioSegundos);
    }

    [Fact]
    public async Task CriarCameraRtsp_Habilitada_SemSenhaNoEquipamento_RecusaComMensagemClara()
    {
        var c = await Preparar(nameof(CriarCameraRtsp_Habilitada_SemSenhaNoEquipamento_RecusaComMensagemClara));
        var eq = await Criar(c, Novo(c, senha: null));
        var handler = new CriarCameraCommandHandler(c.Db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 0), default));
        Assert.Contains("senha do equipamento", ex.Message);
        Assert.Empty(c.Db.Cameras);

        // Desligada pode ser cadastrada: a senha só é exigida para ativar.
        await handler.Handle(new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, false, 30, FonteCamera.Rtsp, eq, 1, 0), default);
        Assert.Single(c.Db.Cameras);
    }

    [Fact]
    public async Task AtualizarCameraRtsp_AtivarSemSenha_Recusa_ComSenhaAtiva()
    {
        var c = await Preparar(nameof(AtualizarCameraRtsp_AtivarSemSenha_Recusa_ComSenhaAtiva));
        var eq = await Criar(c, Novo(c, senha: null));
        var camId = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, false, 30, FonteCamera.Rtsp, eq, 1, 0), default);
        var atualizar = new AtualizarCameraCommandHandler(c.Db);

        // Só liga a câmera, sem reenviar a origem: a senha continua sendo exigida.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            atualizar.Handle(new AtualizarCameraCommand(camId, "L1", null, TipoCamera.Canteiro, true, 30), default));

        await new DefinirSenhaEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(new DefinirSenhaEquipamentoCameraCommand(eq, "ok-123"), default);
        await atualizar.Handle(new AtualizarCameraCommand(camId, "L1", null, TipoCamera.Canteiro, true, 30), default);
        Assert.True((await c.Db.Cameras.SingleAsync()).Habilitada);
    }

    [Fact]
    public async Task CriarCameraRtsp_EquipamentoDeOutraObra_Recusa()
    {
        var c = await Preparar(nameof(CriarCameraRtsp_EquipamentoDeOutraObra_Recusa));
        var eqB = await new CriarEquipamentoCameraCommandHandler(c.Db, Cripto).Handle(
            new CriarEquipamentoCameraCommand(c.ObraB.Id, c.AgenteB.Id, "EB", "10.0.0.2", Senha: "x"), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eqB, 1, 0), default));

        Assert.Contains("outra obra", ex.Message);
    }

    [Fact]
    public async Task CriarCameraRtsp_AgenteDoEquipamentoRevogado_NaoAtiva()
    {
        var c = await Preparar(nameof(CriarCameraRtsp_AgenteDoEquipamentoRevogado_NaoAtiva));
        var eq = await Criar(c, Novo(c));
        c.AgenteA.Ativo = false;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 0), default));
    }

    [Fact]
    public async Task CriarCameraRtsp_MesmaLenteDuasVezes_Recusa()
    {
        var c = await Preparar(nameof(CriarCameraRtsp_MesmaLenteDuasVezes_Recusa));
        var eq = await Criar(c, Novo(c));
        var handler = new CriarCameraCommandHandler(c.Db);
        await handler.Handle(new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 0), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1 de novo", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 1), default));
        await handler.Handle(new CriarCameraCommand(c.ObraA.Id, "L2", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 2, 0), default);
        Assert.Equal(2, c.Db.Cameras.Count());
    }

    [Theory]
    [InlineData(null, 1, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(1, 3, 0)]
    [InlineData(1, 1, 2)]
    public void ValidadorCriarCamera_Rtsp_ExigeEquipamentoCanal1ou2EStream0ou1(int? equipamentoFlag, int canal, int stream)
    {
        var equipamento = equipamentoFlag is null ? (Guid?)null : equipamentoFlag == 0 ? Guid.Empty : Guid.NewGuid();
        var resultado = new CriarCameraCommandValidator().Validate(
            new CriarCameraCommand(Guid.NewGuid(), "C", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, equipamento, canal, stream));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void ValidadorCriarCamera_IntervaloDeEnvioForaDaFaixa_Recusa()
    {
        var v = new CriarCameraCommandValidator();
        var cmd = new CriarCameraCommand(Guid.NewGuid(), "C", null, TipoCamera.Canteiro, true, 30);

        Assert.False(v.Validate(cmd with { IntervaloEnvioSegundos = 0 }).IsValid);
        Assert.False(v.Validate(cmd with { IntervaloEnvioSegundos = 601 }).IsValid);
        Assert.True(v.Validate(cmd with { IntervaloEnvioSegundos = 3 }).IsValid);
    }

    [Fact]
    public async Task AtualizarCamera_SemFonte_NaoMexeNaOrigemDaImagem()
    {
        var c = await Preparar(nameof(AtualizarCamera_SemFonte_NaoMexeNaOrigemDaImagem));
        var eq = await Criar(c, Novo(c));
        var camId = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 2, 1, 7), default);

        await new AtualizarCameraCommandHandler(c.Db).Handle(
            new AtualizarCameraCommand(camId, "Renomeada", null, TipoCamera.Portaria, true, 10), default);

        var cam = await c.Db.Cameras.SingleAsync();
        Assert.Equal("Renomeada", cam.Nome);
        Assert.Equal(FonteCamera.Rtsp, cam.Fonte);
        Assert.Equal(eq, cam.EquipamentoCameraId);
        Assert.Equal(2, cam.Canal);
        Assert.Equal(1, cam.Stream);
        Assert.Equal(7, cam.IntervaloEnvioSegundos);
    }

    [Fact]
    public async Task AtualizarCamera_VoltaParaNavegador_LimpaEquipamentoCanalEStream()
    {
        var c = await Preparar(nameof(AtualizarCamera_VoltaParaNavegador_LimpaEquipamentoCanalEStream));
        var eq = await Criar(c, Novo(c));
        var camId = await new CriarCameraCommandHandler(c.Db).Handle(
            new CriarCameraCommand(c.ObraA.Id, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 0), default);

        await new AtualizarCameraCommandHandler(c.Db).Handle(
            new AtualizarCameraCommand(camId, "L1", null, TipoCamera.Canteiro, true, 30, FonteCamera.Navegador), default);

        var cam = await c.Db.Cameras.SingleAsync();
        Assert.Equal(FonteCamera.Navegador, cam.Fonte);
        Assert.Null(cam.EquipamentoCameraId);
        Assert.Null(cam.Canal);
        Assert.Null(cam.Stream);
    }

    // ---------- Lista de câmeras: situação calculada ----------

    [Fact]
    public async Task ListarCameras_CalculaASituacaoDaCameraIp_ENaoEscondeCameraComEquipamentoInativado()
    {
        var c = await Preparar(nameof(ListarCameras_CalculaASituacaoDaCameraIp_ENaoEscondeCameraComEquipamentoInativado));
        var eq = await Criar(c, Novo(c));
        var criar = new CriarCameraCommandHandler(c.Db);
        var recebendo = await criar.Handle(new CriarCameraCommand(c.ObraA.Id, "Recebendo", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 1, 0), default);
        var aguardando = await criar.Handle(new CriarCameraCommand(c.ObraA.Id, "Aguardando", null, TipoCamera.Canteiro, true, 30, FonteCamera.Rtsp, eq, 2, 0), default);
        var navegador = await criar.Handle(new CriarCameraCommand(c.ObraA.Id, "Tablet", null, TipoCamera.Canteiro, true, 30), default);
        c.AgenteA.UltimoContatoEmUtc = DateTime.UtcNow.AddSeconds(-20);
        (await c.Db.Cameras.SingleAsync(x => x.Id == recebendo)).UltimoQuadroRecebidoEmUtc = DateTime.UtcNow.AddMinutes(-1);
        await c.Db.SaveChangesAsync();

        var lista = await new ListarCamerasQueryHandler(c.Db).Handle(new ListarCamerasQuery(null), default);

        Assert.Equal(SituacaoConexaoCameraRegras.RecebendoImagem, lista.Single(x => x.Id == recebendo).SituacaoConexao);
        Assert.Equal(SituacaoConexaoCameraRegras.AguardandoSinal, lista.Single(x => x.Id == aguardando).SituacaoConexao);
        Assert.Null(lista.Single(x => x.Id == navegador).SituacaoConexao);
        Assert.Equal("A31 portaria", lista.Single(x => x.Id == recebendo).EquipamentoNome);

        // Agente sem contato há mais de 3 min: mesmo com quadro recente.
        c.AgenteA.UltimoContatoEmUtc = DateTime.UtcNow.AddMinutes(-4);
        await c.Db.SaveChangesAsync();
        lista = await new ListarCamerasQueryHandler(c.Db).Handle(new ListarCamerasQuery(null), default);
        Assert.Equal(SituacaoConexaoCameraRegras.AgenteSemContato, lista.Single(x => x.Id == recebendo).SituacaoConexao);

        // Equipamento inativado por fora (não pela exclusão com trava): a câmera continua na lista (sem INNER JOIN).
        (await c.Db.EquipamentosCamera.SingleAsync()).Ativo = false;
        await c.Db.SaveChangesAsync();
        lista = await new ListarCamerasQueryHandler(c.Db).Handle(new ListarCamerasQuery(null), default);
        Assert.Equal(3, lista.Count);
    }

    [Theory]
    [InlineData(FonteCamera.Navegador, true, true, 10, 10, null)]
    [InlineData(FonteCamera.Rtsp, false, true, 10, 10, null)]
    [InlineData(FonteCamera.Rtsp, true, false, 10, 10, SituacaoConexaoCameraRegras.AgenteSemContato)]
    [InlineData(FonteCamera.Rtsp, true, true, null, 10, SituacaoConexaoCameraRegras.AgenteSemContato)]
    [InlineData(FonteCamera.Rtsp, true, true, 181, 10, SituacaoConexaoCameraRegras.AgenteSemContato)]
    [InlineData(FonteCamera.Rtsp, true, true, 179, null, SituacaoConexaoCameraRegras.AguardandoSinal)]
    [InlineData(FonteCamera.Rtsp, true, true, 60, 901, SituacaoConexaoCameraRegras.AguardandoSinal)]
    [InlineData(FonteCamera.Rtsp, true, true, 60, 899, SituacaoConexaoCameraRegras.RecebendoImagem)]
    public void Situacao_Calcular(FonteCamera fonte, bool habilitada, bool agenteAtivo, int? segundosDesdeContato, int? segundosDesdeQuadro, string? esperado)
    {
        var agora = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var resultado = SituacaoConexaoCameraRegras.Calcular(fonte, habilitada, agenteAtivo,
            segundosDesdeContato is { } c ? agora.AddSeconds(-c) : null, segundosDesdeQuadro is { } q ? agora.AddSeconds(-q) : null, agora);

        Assert.Equal(esperado, resultado);
    }
}
