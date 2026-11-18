using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Rota anônima do agente: configuração (com a senha decifrada) e teste de conexão. O contexto "anônimo" nasce com o escopo
// ZERADO, exatamente como o EscopoPorObraMiddleware o deixa com o Entra ligado (produção/hml): se alguma consulta dependesse
// do filtro global de RBAC ou do soft delete, ela voltaria vazia aqui e o teste falharia (em dev, com escopo global, nunca falharia).
public class ConfiguracaoAgenteCameraTests
{
    private static readonly ISegredoDispositivoHasher Hasher = new SegredoDispositivoHasherService();
    private static readonly ICredencialCameraCriptografia Cripto = new CredencialCameraCriptografiaService();

    public ConfiguracaoAgenteCameraTests()
    {
        ChaveCredencialCameraDeTeste.Configurar();
        ChavesCpfDeTeste.Configurar(); // a miniatura do teste é cifrada com a chave das fotos
    }

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Anonimo, CurrentUserService Escopo, Obra ObraA, Obra ObraB, AgenteCamera AgenteA, AgenteCamera AgenteB,
        string SegredoA, EquipamentoCamera EquipA, Camera Lente1, Camera Lente2)
    {
        // Cada requisição real tem o próprio DbContext; nos testes, reusar o mesmo exige largar o que ele já leu (instância em cache).
        public void NovaRequisicao()
        {
            Global.ChangeTracker.Clear();
            Anonimo.ChangeTracker.Clear();
        }

        public ObterConfiguracaoAgenteCameraQueryHandler Config() => new(Anonimo, new AgenteCameraAutenticador(Anonimo, Hasher), Escopo, Cripto,
            NullLogger<ObterConfiguracaoAgenteCameraQueryHandler>.Instance);

        public RegistrarResultadoTesteConexaoCameraCommandHandler Resultado() => new(Anonimo, new AgenteCameraAutenticador(Anonimo, Hasher), Escopo,
            NullLogger<RegistrarResultadoTesteConexaoCameraCommandHandler>.Instance);
    }

    private static async Task<Cenario> Preparar(string nomeBanco)
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var segredoA = Hasher.GerarSegredo();
        var agenteA = new AgenteCamera { ObraId = obraA.Id, Nome = "PC A", SegredoHash = Hasher.GerarHash(segredoA) };
        var agenteB = new AgenteCamera { ObraId = obraB.Id, Nome = "PC B", SegredoHash = Hasher.GerarHash(Hasher.GerarSegredo()) };
        var equipA = new EquipamentoCamera
        {
            ObraId = obraA.Id, AgenteCameraId = agenteA.Id, Nome = "A31", Host = "192.168.1.50", PortaRtsp = 554, PortaOnvif = 8899,
            EnderecoMac = "AA:BB:CC:DD:EE:FF", Usuario = "admin", SenhaCifrada = Cripto.Cifrar("senha-da-camera"),
        };
        var lente1 = new Camera
        {
            ObraId = obraA.Id, Nome = "Lente 1", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipA.Id, Canal = 1, Stream = 1, IntervaloEnvioSegundos = 3,
        };
        var lente2 = new Camera
        {
            ObraId = obraA.Id, Nome = "Lente 2", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipA.Id, Canal = 2, Stream = 0, IntervaloEnvioSegundos = 5,
        };
        global.AddRange(obraA, obraB, agenteA, agenteB, equipA, lente1, lente2);
        await global.SaveChangesAsync();

        var escopo = new CurrentUserService();
        escopo.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
        var anonimo = new SstDbContext(opcoes, escopo);
        return new Cenario(global, anonimo, escopo, obraA, obraB, agenteA, agenteB, segredoA, equipA, lente1, lente2);
    }

    private static ObterConfiguracaoAgenteCameraQuery Pedir(Cenario c, string? segredo = null, Guid? agenteId = null)
        => new(agenteId ?? c.AgenteA.Id, segredo ?? c.SegredoA, "1.2.3");

    [Fact]
    public async Task EscopoZerado_DevolveAsCamerasIpDoAgente_ComASenhaDecifrada()
    {
        var c = await Preparar(nameof(EscopoZerado_DevolveAsCamerasIpDoAgente_ComASenhaDecifrada));
        Assert.Empty(await c.Anonimo.Cameras.ToListAsync()); // prova do problema: o filtro global esconde tudo neste escopo

        var config = await c.Config().Handle(Pedir(c), default);

        Assert.NotNull(config);
        Assert.Equal(2, config.Cameras.Count);
        var l1 = config.Cameras.Single(x => x.CameraId == c.Lente1.Id);
        Assert.Equal("192.168.1.50", l1.Host);
        Assert.Equal(554, l1.PortaRtsp);
        Assert.Equal(8899, l1.PortaOnvif);
        Assert.Equal("admin", l1.Usuario);
        Assert.Equal("senha-da-camera", l1.Senha);
        Assert.Equal(1, l1.Canal);
        Assert.Equal(1, l1.Stream);
        Assert.Equal(3, l1.IntervaloEnvioSegundos);
        var l2 = config.Cameras.Single(x => x.CameraId == c.Lente2.Id);
        Assert.Equal(2, l2.Canal);
        Assert.Equal(0, l2.Stream);
        Assert.Equal(5, l2.IntervaloEnvioSegundos);
        Assert.Equal(15, config.IntervaloConsultaSegundos);
        Assert.Empty(config.TestesConexao);
    }

    [Fact]
    public async Task SegredoErradoOuAgenteInexistenteOuRevogado_DevolveNulo()
    {
        var c = await Preparar(nameof(SegredoErradoOuAgenteInexistenteOuRevogado_DevolveNulo));
        var handler = c.Config();

        Assert.Null(await handler.Handle(Pedir(c, segredo: "errado"), default));
        Assert.Null(await handler.Handle(Pedir(c, segredo: ""), default));
        Assert.Null(await c.Config().Handle(Pedir(c, agenteId: Guid.NewGuid()), default));

        c.AgenteA.Ativo = false;
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();
        Assert.Null(await c.Config().Handle(Pedir(c), default));
    }

    [Fact]
    public async Task ObraInativa_NaoAutentica()
    {
        var c = await Preparar(nameof(ObraInativa_NaoAutentica));
        c.ObraA.Ativo = false;
        await c.Global.SaveChangesAsync();

        Assert.Null(await c.Config().Handle(Pedir(c), default));
    }

    [Fact]
    public async Task NaoDevolveCameraDeOutroAgenteNemDeOutraObra()
    {
        var c = await Preparar(nameof(NaoDevolveCameraDeOutroAgenteNemDeOutraObra));
        var equipB = new EquipamentoCamera { ObraId = c.ObraB.Id, AgenteCameraId = c.AgenteB.Id, Nome = "B1", Host = "10.0.0.9", SenhaCifrada = Cripto.Cifrar("segredo-da-obra-b") };
        var camB = new Camera { ObraId = c.ObraB.Id, Nome = "Cam B", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipB.Id, Canal = 1, Stream = 0 };
        // Outro equipamento da MESMA obra, mas de outro agente.
        var agenteA2 = new AgenteCamera { ObraId = c.ObraA.Id, Nome = "PC A2", SegredoHash = "x" };
        var equipA2 = new EquipamentoCamera { ObraId = c.ObraA.Id, AgenteCameraId = agenteA2.Id, Nome = "A2", Host = "10.0.0.8", SenhaCifrada = Cripto.Cifrar("do-outro-agente") };
        var camA2 = new Camera { ObraId = c.ObraA.Id, Nome = "Cam A2", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipA2.Id, Canal = 1, Stream = 0 };
        c.Global.AddRange(equipB, camB, agenteA2, equipA2, camA2);
        await c.Global.SaveChangesAsync();

        var config = await c.Config().Handle(Pedir(c), default);

        Assert.NotNull(config);
        Assert.DoesNotContain(config.Cameras, x => x.CameraId == camB.Id || x.CameraId == camA2.Id);
        var json = JsonSerializer.Serialize(config);
        Assert.DoesNotContain("segredo-da-obra-b", json);
        Assert.DoesNotContain("do-outro-agente", json);
    }

    [Fact]
    public async Task NaoDevolveCameraDesligadaExcluidaDoNavegadorOuDeEquipamentoExcluidoOuSemSenha()
    {
        var c = await Preparar(nameof(NaoDevolveCameraDesligadaExcluidaDoNavegadorOuDeEquipamentoExcluidoOuSemSenha));
        c.Lente1.Habilitada = false;                                  // desligada
        c.Lente2.Ativo = false;                                       // excluída (soft delete)
        var navegador = new Camera { ObraId = c.ObraA.Id, Nome = "Tablet" };
        var equipSemSenha = new EquipamentoCamera { ObraId = c.ObraA.Id, AgenteCameraId = c.AgenteA.Id, Nome = "Sem senha", Host = "10.0.0.7" };
        var camSemSenha = new Camera { ObraId = c.ObraA.Id, Nome = "Cam sem senha", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipSemSenha.Id, Canal = 1, Stream = 0 };
        var equipExcluido = new EquipamentoCamera { ObraId = c.ObraA.Id, AgenteCameraId = c.AgenteA.Id, Nome = "Excluído", Host = "10.0.0.6", Ativo = false, SenhaCifrada = Cripto.Cifrar("x") };
        var camDoExcluido = new Camera { ObraId = c.ObraA.Id, Nome = "Cam do excluído", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = equipExcluido.Id, Canal = 1, Stream = 0 };
        c.Global.AddRange(navegador, equipSemSenha, camSemSenha, equipExcluido, camDoExcluido);
        await c.Global.SaveChangesAsync();

        var config = await c.Config().Handle(Pedir(c), default);

        Assert.NotNull(config);
        Assert.Empty(config.Cameras);
    }

    [Fact]
    public async Task SenhaQueNaoDecifra_CameraFicaForaDaConfiguracao()
    {
        var c = await Preparar(nameof(SenhaQueNaoDecifra_CameraFicaForaDaConfiguracao));
        c.EquipA.SenhaCifrada = "isto-nao-e-uma-cifra-valida";
        await c.Global.SaveChangesAsync();

        var config = await c.Config().Handle(Pedir(c), default);

        Assert.NotNull(config);
        Assert.Empty(config.Cameras);
    }

    [Fact]
    public async Task RegistraOContatoEAVersaoDoAgente()
    {
        var c = await Preparar(nameof(RegistraOContatoEAVersaoDoAgente));
        Assert.Null(c.AgenteA.UltimoContatoEmUtc);

        await c.Config().Handle(Pedir(c), default);

        var lido = await c.Global.AgentesCamera.AsNoTracking().SingleAsync(a => a.Id == c.AgenteA.Id);
        Assert.NotNull(lido.UltimoContatoEmUtc);
        Assert.Equal("1.2.3", lido.Versao);
    }

    [Fact]
    public void ToStringDosDtosComSenha_NaoImprimeASenha()
    {
        var dto = new CameraDoAgenteDto(Guid.NewGuid(), "C", Guid.NewGuid(), "10.0.0.1", 554, 8899, null, "admin", "senha-secreta", 1, 0, 3);
        var teste = new TesteConexaoDoAgenteDto(Guid.NewGuid(), Guid.NewGuid(), null, "10.0.0.1", 554, 8899, null, "admin", "senha-secreta", 1, 0);

        Assert.DoesNotContain("senha-secreta", dto.ToString());
        Assert.DoesNotContain("senha-secreta", teste.ToString());
        Assert.DoesNotContain("senha-secreta", new CriarEquipamentoCameraCommand(Guid.NewGuid(), Guid.NewGuid(), "E", "10.0.0.1", Senha: "senha-secreta").ToString());
        Assert.DoesNotContain("senha-secreta", new DefinirSenhaEquipamentoCameraCommand(Guid.NewGuid(), "senha-secreta").ToString());
    }

    // ---------- Teste de conexão ----------

    private static Task<Guid> Solicitar(Cenario c, SolicitarTesteConexaoCameraCommand cmd)
        => new SolicitarTesteConexaoCameraCommandHandler(c.Global).Handle(cmd, default);

    [Fact]
    public async Task TesteDeConexao_PedidoVaiNaConfiguracaoDoAgente_EOResultadoVoltaAoFront()
    {
        var c = await Preparar(nameof(TesteDeConexao_PedidoVaiNaConfiguracaoDoAgente_EOResultadoVoltaAoFront));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente2.Id));

        // Front: ainda pendente.
        var consulta = new ObterTesteConexaoCameraQueryHandler(c.Global);
        var pendente = await consulta.Handle(new ObterTesteConexaoCameraQuery(testeId), default);
        Assert.Equal("Pendente", pendente.Situacao);
        Assert.Null(pendente.Sucesso);

        // Agente: recebe o pedido junto da configuração (com a conexão da lente 2 e a senha).
        var config = await c.Config().Handle(Pedir(c), default);
        var pedido = Assert.Single(config!.TestesConexao);
        Assert.Equal(testeId, pedido.TesteId);
        Assert.Equal(c.Lente2.Id, pedido.CameraId);
        Assert.Equal(2, pedido.Canal);
        Assert.Equal(0, pedido.Stream);
        Assert.Equal("192.168.1.50", pedido.Host);
        Assert.Equal("senha-da-camera", pedido.Senha);
        Assert.NotNull((await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync()).EntregueAoAgenteEmUtc);

        // Agente: devolve o resultado com miniatura.
        var jpeg = new byte[] { 0xFF, 0xD8, 1, 2, 3 };
        var ok = await c.Resultado().Handle(new RegistrarResultadoTesteConexaoCameraCommand(
            c.AgenteA.Id, c.SegredoA, "1.2.3", testeId, true, "Imagem recebida", "1280x720 H.264", jpeg), default);
        Assert.True(ok);
        c.NovaRequisicao();

        // Front: concluído, com a miniatura UMA vez; depois ela é apagada.
        var concluido = await consulta.Handle(new ObterTesteConexaoCameraQuery(testeId), default);
        Assert.Equal("Concluido", concluido.Situacao);
        Assert.True(concluido.Sucesso);
        Assert.Equal("1280x720 H.264", concluido.Detalhe);
        Assert.Equal(Convert.ToBase64String(jpeg), concluido.MiniaturaBase64);
        var segunda = await consulta.Handle(new ObterTesteConexaoCameraQuery(testeId), default);
        Assert.Null(segunda.MiniaturaBase64);
        Assert.Null((await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync()).Miniatura);

        // Concluído não volta a ser entregue ao agente.
        Assert.Empty((await c.Config().Handle(Pedir(c), default))!.TestesConexao);
    }

    [Fact]
    public async Task TesteDeConexao_PorEquipamento_UsaCanalEStreamInformados()
    {
        var c = await Preparar(nameof(TesteDeConexao_PorEquipamento_UsaCanalEStreamInformados));

        await Solicitar(c, new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.EquipA.Id, Canal: 2, Stream: 1));

        var config = await c.Config().Handle(Pedir(c), default);
        var pedido = Assert.Single(config!.TestesConexao);
        Assert.Null(pedido.CameraId);
        Assert.Equal(2, pedido.Canal);
        Assert.Equal(1, pedido.Stream);
    }

    [Fact]
    public async Task ParteDaImagem_VaiEmCadaCameraEEmCadaTesteDeConexao_DaConfiguracaoDoAgente()
    {
        var c = await Preparar(nameof(ParteDaImagem_VaiEmCadaCameraEEmCadaTesteDeConexao_DaConfiguracaoDoAgente));
        var lente2Baixo = new Camera
        {
            ObraId = c.ObraA.Id, Nome = "Lente 2 baixo", Fonte = FonteCamera.Rtsp, EquipamentoCameraId = c.EquipA.Id, Canal = 2, Stream = 0,
            ParteImagem = ParteImagemCamera.MetadeInferior, AvisarTelegram = false,
        };
        c.Lente2.ParteImagem = ParteImagemCamera.MetadeSuperior;
        c.Global.Add(lente2Baixo);
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();

        await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: lente2Baixo.Id));
        await Solicitar(c, new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: c.EquipA.Id, Canal: 1, Stream: 0,
            ParteImagem: ParteImagemCamera.MetadeSuperior));

        var config = await c.Config().Handle(Pedir(c), default);

        Assert.NotNull(config);
        Assert.Equal(ParteImagemCamera.Inteira, config.Cameras.Single(x => x.CameraId == c.Lente1.Id).ParteImagem);
        Assert.Equal(ParteImagemCamera.MetadeSuperior, config.Cameras.Single(x => x.CameraId == c.Lente2.Id).ParteImagem);
        Assert.Equal(ParteImagemCamera.MetadeInferior, config.Cameras.Single(x => x.CameraId == lente2Baixo.Id).ParteImagem);
        Assert.Equal(2, config.TestesConexao.Count);
        Assert.Equal(ParteImagemCamera.MetadeInferior, config.TestesConexao.Single(t => t.CameraId == lente2Baixo.Id).ParteImagem);
        Assert.Equal(ParteImagemCamera.MetadeSuperior, config.TestesConexao.Single(t => t.CameraId is null).ParteImagem);

        // No JSON que o agente lê, o campo sai como número (0|1|2), como o contrato manda.
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"parteImagem\":2", json);
        Assert.Contains("\"parteImagem\":1", json);
        Assert.Contains("\"parteImagem\":0", json);
    }

    [Fact]
    public async Task TesteDeConexao_Falha_GuardaMotivo_ESemMiniatura()
    {
        var c = await Preparar(nameof(TesteDeConexao_Falha_GuardaMotivo_ESemMiniatura));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));

        await c.Resultado().Handle(new RegistrarResultadoTesteConexaoCameraCommand(
            c.AgenteA.Id, c.SegredoA, null, testeId, false, "Senha recusada pela câmera.", null, new byte[] { 0xFF, 0xD8, 1 }), default);
        c.NovaRequisicao();

        var r = await new ObterTesteConexaoCameraQueryHandler(c.Global).Handle(new ObterTesteConexaoCameraQuery(testeId), default);
        Assert.Equal("Concluido", r.Situacao);
        Assert.False(r.Sucesso);
        Assert.Equal("Senha recusada pela câmera.", r.Motivo);
        Assert.Null(r.MiniaturaBase64);
    }

    [Fact]
    public async Task TesteDeConexao_PedidoDuploEmAberto_ReaproveitaOMesmo()
    {
        var c = await Preparar(nameof(TesteDeConexao_PedidoDuploEmAberto_ReaproveitaOMesmo));

        var a = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        var b = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        var outraLente = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente2.Id));

        Assert.Equal(a, b);
        Assert.NotEqual(a, outraLente);
    }

    [Fact]
    public async Task TesteDeConexao_SemRespostaNoPrazo_Expira_ENaoVaiMaisAoAgente()
    {
        var c = await Preparar(nameof(TesteDeConexao_SemRespostaNoPrazo_Expira_ENaoVaiMaisAoAgente));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        var teste = await c.Global.TestesConexaoCamera.SingleAsync();
        teste.SolicitadoEmUtc = DateTime.UtcNow.AddMinutes(-3);
        await c.Global.SaveChangesAsync();

        var r = await new ObterTesteConexaoCameraQueryHandler(c.Global).Handle(new ObterTesteConexaoCameraQuery(testeId), default);

        Assert.Equal("Expirado", r.Situacao);
        Assert.Contains("agente", r.Motivo);
        Assert.Empty((await c.Config().Handle(Pedir(c), default))!.TestesConexao);
    }

    [Fact]
    public async Task TesteDeConexao_CameraDoNavegador_Recusa()
    {
        var c = await Preparar(nameof(TesteDeConexao_CameraDoNavegador_Recusa));
        var tablet = new Camera { ObraId = c.ObraA.Id, Nome = "Tablet" };
        c.Global.Add(tablet);
        await c.Global.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: tablet.Id)));
    }

    [Fact]
    public async Task TesteDeConexao_AgenteRevogado_NaoPodeSerPedido()
    {
        var c = await Preparar(nameof(TesteDeConexao_AgenteRevogado_NaoPodeSerPedido));
        c.AgenteA.Ativo = false;
        await c.Global.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id)));
    }

    [Fact]
    public void TesteDeConexao_Validador_ExigeCameraOuEquipamento_UmDosDois()
    {
        var v = new SolicitarTesteConexaoCameraCommandValidator();

        Assert.False(v.Validate(new SolicitarTesteConexaoCameraCommand()).IsValid);
        Assert.False(v.Validate(new SolicitarTesteConexaoCameraCommand(CameraId: Guid.NewGuid(), EquipamentoCameraId: Guid.NewGuid())).IsValid);
        Assert.False(v.Validate(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: Guid.NewGuid(), Canal: 3)).IsValid);
        Assert.True(v.Validate(new SolicitarTesteConexaoCameraCommand(EquipamentoCameraId: Guid.NewGuid(), Canal: 2, Stream: 1)).IsValid);
    }

    [Fact]
    public async Task Resultado_SegredoErrado_DevolveFalse_ETesteDeOutroAgente_NaoEncontrado()
    {
        var c = await Preparar(nameof(Resultado_SegredoErrado_DevolveFalse_ETesteDeOutroAgente_NaoEncontrado));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));

        Assert.False(await c.Resultado().Handle(new RegistrarResultadoTesteConexaoCameraCommand(
            c.AgenteA.Id, "errado", null, testeId, true, null, null, null), default));

        // O agente B (obra B), mesmo autenticado, não alcança o teste do agente A.
        var segredoB = Hasher.GerarSegredo();
        c.AgenteB.SegredoHash = Hasher.GerarHash(segredoB);
        await c.Global.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => c.Resultado().Handle(new RegistrarResultadoTesteConexaoCameraCommand(
            c.AgenteB.Id, segredoB, null, testeId, true, null, null, null), default));
    }

    [Fact]
    public async Task Resultado_Reenviado_MantemOPrimeiro()
    {
        var c = await Preparar(nameof(Resultado_Reenviado_MantemOPrimeiro));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        var handler = c.Resultado();

        await handler.Handle(new RegistrarResultadoTesteConexaoCameraCommand(c.AgenteA.Id, c.SegredoA, null, testeId, true, "primeiro", null, null), default);
        c.NovaRequisicao();
        Assert.True(await c.Resultado().Handle(new RegistrarResultadoTesteConexaoCameraCommand(c.AgenteA.Id, c.SegredoA, null, testeId, false, "segundo", null, null), default));

        var t = await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync();
        Assert.True(t.Sucesso);
        Assert.Equal("primeiro", t.Motivo);
    }

    [Fact]
    public void Resultado_Validador_RecusaMiniaturaGrandeOuQueNaoEJpeg()
    {
        var v = new RegistrarResultadoTesteConexaoCameraCommandValidator();
        RegistrarResultadoTesteConexaoCameraCommand Com(byte[] m) => new(Guid.NewGuid(), "s", null, Guid.NewGuid(), true, null, null, m);

        Assert.False(v.Validate(Com(new byte[] { 1, 2, 3 })).IsValid);
        Assert.False(v.Validate(Com(new byte[RegistrarResultadoTesteConexaoCameraCommandValidator.TamanhoMaximoMiniaturaBytes + 1].Select((_, i) => i < 2 ? (byte)(i == 0 ? 0xFF : 0xD8) : (byte)0).ToArray())).IsValid);
        Assert.True(v.Validate(Com(new byte[] { 0xFF, 0xD8, 1 })).IsValid);
    }

    // ---------- Último quadro recebido ----------

    [Fact]
    public async Task QuadroDoAgente_AtualizaOUltimoQuadroESituacaoDaCamera()
    {
        var c = await Preparar(nameof(QuadroDoAgente_AtualizaOUltimoQuadroESituacaoDaCamera));
        Assert.Null(c.Lente1.UltimoQuadroRecebidoEmUtc);
        var sender = new SenderQueApenasConfirma();
        var handler = new AnalisarQuadroAgenteCameraCommandHandler(c.Anonimo, new AgenteCameraAutenticador(c.Anonimo, Hasher), c.Escopo, sender,
            NullLogger<AnalisarQuadroAgenteCameraCommandHandler>.Instance);

        await handler.Handle(new AnalisarQuadroAgenteCameraCommand(c.AgenteA.Id, c.SegredoA, "1.0", c.Lente1.Id, new byte[] { 0xFF, 0xD8, 1 }, DateTime.UtcNow), default);

        var cam = await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.Lente1.Id);
        Assert.Equal(SituacaoConexaoCamera.RecebendoImagem, cam.SituacaoConexao);
        Assert.NotNull(cam.UltimoQuadroRecebidoEmUtc);
        Assert.True(DateTime.UtcNow - cam.UltimoQuadroRecebidoEmUtc < TimeSpan.FromMinutes(1));
        Assert.Equal(1, sender.Chamadas);
    }

    // ---------- Código do motivo ----------

    private static RegistrarResultadoTesteConexaoCameraCommand Resultado(Cenario c, Guid testeId, bool sucesso, string? codigo, string? motivo = null, byte[]? miniatura = null)
        => new(c.AgenteA.Id, c.SegredoA, null, testeId, sucesso, motivo, null, miniatura, codigo);

    [Theory]
    [InlineData("senha_recusada", "senha_recusada")]
    [InlineData("  Sem_Resposta ", "sem_resposta")]
    [InlineData("ffmpeg_ausente", "ffmpeg_ausente")]
    [InlineData("inventado", "erro_desconhecido")]
    [InlineData("A câmera não respondeu a tempo", "erro_desconhecido")]
    [InlineData("", "erro_desconhecido")]
    [InlineData(null, "erro_desconhecido")]
    public async Task Resultado_Falha_GuardaOCodigoConhecido_ECodigoDesconhecidoViraErroDesconhecido(string? enviado, string esperado)
    {
        var c = await Preparar("Codigo_" + Guid.NewGuid());
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));

        await c.Resultado().Handle(Resultado(c, testeId, false, enviado, "Texto livre"), default);
        c.NovaRequisicao();

        var r = await new ObterTesteConexaoCameraQueryHandler(c.Global).Handle(new ObterTesteConexaoCameraQuery(testeId), default);
        Assert.Equal(esperado, r.Codigo);
        Assert.Equal("Texto livre", r.Motivo);
    }

    [Fact]
    public async Task Resultado_Sucesso_NaoTemCodigo_ECodigoNaoVaiNoPendenteNemNoExpirado()
    {
        var c = await Preparar(nameof(Resultado_Sucesso_NaoTemCodigo_ECodigoNaoVaiNoPendenteNemNoExpirado));
        var ok = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        await c.Resultado().Handle(Resultado(c, ok, true, "senha_recusada"), default);
        c.NovaRequisicao();
        var consulta = new ObterTesteConexaoCameraQueryHandler(c.Global);

        Assert.Null((await consulta.Handle(new ObterTesteConexaoCameraQuery(ok), default)).Codigo);

        var pendente = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente2.Id));
        Assert.Null((await consulta.Handle(new ObterTesteConexaoCameraQuery(pendente), default)).Codigo);

        var teste = await c.Global.TestesConexaoCamera.SingleAsync(t => t.Id == pendente);
        teste.SolicitadoEmUtc = DateTime.UtcNow.AddMinutes(-3);
        teste.Codigo = "sem_resposta";
        await c.Global.SaveChangesAsync();
        var expirado = await consulta.Handle(new ObterTesteConexaoCameraQuery(pendente), default);
        Assert.Equal("Expirado", expirado.Situacao);
        Assert.Null(expirado.Codigo);
    }

    // ---------- Miniatura sem prazo ----------

    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 1, 2, 3 };

    [Fact]
    public async Task Resultado_ChegandoDepoisDeExpirarOPedido_NaoGravaAMiniatura()
    {
        var c = await Preparar(nameof(Resultado_ChegandoDepoisDeExpirarOPedido_NaoGravaAMiniatura));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));
        var teste = await c.Global.TestesConexaoCamera.SingleAsync();
        teste.SolicitadoEmUtc = DateTime.UtcNow.AddMinutes(-3);
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();

        Assert.True(await c.Resultado().Handle(Resultado(c, testeId, true, null, "ok", Jpeg), default));

        var gravado = await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync();
        Assert.True(gravado.Sucesso);
        Assert.Null(gravado.Miniatura);
    }

    [Fact]
    public async Task Resultado_DentroDoPrazo_GuardaAMiniatura()
    {
        var c = await Preparar(nameof(Resultado_DentroDoPrazo_GuardaAMiniatura));
        var testeId = await Solicitar(c, new SolicitarTesteConexaoCameraCommand(CameraId: c.Lente1.Id));

        await c.Resultado().Handle(Resultado(c, testeId, true, null, "ok", Jpeg), default);

        Assert.Equal(Jpeg, (await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync()).Miniatura);
    }

    private static async Task<(Guid Antigo, Guid Recente)> SemearMiniaturas(Cenario c)
    {
        var antigo = new TesteConexaoCamera
        {
            ObraId = c.ObraA.Id, AgenteCameraId = c.AgenteA.Id, EquipamentoCameraId = c.EquipA.Id, Canal = 1, Stream = 0,
            SolicitadoEmUtc = DateTime.UtcNow.AddMinutes(-11), ConcluidoEmUtc = DateTime.UtcNow.AddMinutes(-11), Sucesso = true, Miniatura = Jpeg,
        };
        var recente = new TesteConexaoCamera
        {
            ObraId = c.ObraA.Id, AgenteCameraId = c.AgenteA.Id, EquipamentoCameraId = c.EquipA.Id, Canal = 2, Stream = 0,
            SolicitadoEmUtc = DateTime.UtcNow.AddMinutes(-1), ConcluidoEmUtc = DateTime.UtcNow, Sucesso = true, Miniatura = Jpeg,
        };
        c.Global.AddRange(antigo, recente);
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();
        return (antigo.Id, recente.Id);
    }

    [Fact]
    public async Task ConsultaDoTeste_LimpaMiniaturasAntigasDoAgente_ENuncaEntregaUmaAntiga()
    {
        var c = await Preparar(nameof(ConsultaDoTeste_LimpaMiniaturasAntigasDoAgente_ENuncaEntregaUmaAntiga));
        var (antigo, recente) = await SemearMiniaturas(c);

        // Consultar o recente também limpa o antigo do mesmo agente (sem job).
        var r = await new ObterTesteConexaoCameraQueryHandler(c.Global).Handle(new ObterTesteConexaoCameraQuery(recente), default);
        Assert.Equal(Convert.ToBase64String(Jpeg), r.MiniaturaBase64);
        Assert.Null((await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync(t => t.Id == antigo)).Miniatura);

        // Consultar o próprio teste antigo nunca entrega a miniatura.
        var c2 = await Preparar(nameof(ConsultaDoTeste_LimpaMiniaturasAntigasDoAgente_ENuncaEntregaUmaAntiga) + "_2");
        var (antigo2, _) = await SemearMiniaturas(c2);
        var r2 = await new ObterTesteConexaoCameraQueryHandler(c2.Global).Handle(new ObterTesteConexaoCameraQuery(antigo2), default);
        Assert.Null(r2.MiniaturaBase64);
        Assert.Null((await c2.Global.TestesConexaoCamera.AsNoTracking().SingleAsync(t => t.Id == antigo2)).Miniatura);
    }

    [Fact]
    public async Task ConsultaDeConfiguracaoDoAgente_LimpaMiniaturasAntigas_EMantemAsRecentes()
    {
        var c = await Preparar(nameof(ConsultaDeConfiguracaoDoAgente_LimpaMiniaturasAntigas_EMantemAsRecentes));
        var (antigo, recente) = await SemearMiniaturas(c);

        Assert.NotNull(await c.Config().Handle(Pedir(c), default));

        c.NovaRequisicao();
        Assert.Null((await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync(t => t.Id == antigo)).Miniatura);
        Assert.NotNull((await c.Global.TestesConexaoCamera.AsNoTracking().SingleAsync(t => t.Id == recente)).Miniatura);
    }

    // ---------- Quadro: equipamento do agente e horário de captura ----------

    private static AnalisarQuadroAgenteCameraCommandHandler HandlerQuadro(Cenario c)
        => new(c.Anonimo, new AgenteCameraAutenticador(c.Anonimo, Hasher), c.Escopo, new SenderQueApenasConfirma(),
            NullLogger<AnalisarQuadroAgenteCameraCommandHandler>.Instance);

    private static AnalisarQuadroAgenteCameraCommand Quadro(Cenario c, Guid cameraId, DateTime? capturadaEm = null)
        => new(c.AgenteA.Id, c.SegredoA, "1.0", cameraId, new byte[] { 0xFF, 0xD8, 1 }, capturadaEm ?? DateTime.UtcNow);

    [Fact]
    public async Task Quadro_DeCameraComEquipamentoDeOutroAgente_Recusa404()
    {
        var c = await Preparar(nameof(Quadro_DeCameraComEquipamentoDeOutroAgente_Recusa404));
        var outroAgente = new AgenteCamera { ObraId = c.ObraA.Id, Nome = "PC A2", SegredoHash = Hasher.GerarHash(Hasher.GerarSegredo()) };
        c.Global.Add(outroAgente);
        var equipamento = await c.Global.EquipamentosCamera.SingleAsync(e => e.Id == c.EquipA.Id);
        equipamento.AgenteCameraId = outroAgente.Id; // equipamento (e as lentes) movidos para outro agente da mesma obra
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => HandlerQuadro(c).Handle(Quadro(c, c.Lente1.Id), default));
        Assert.Null((await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.Lente1.Id)).UltimoQuadroRecebidoEmUtc);
    }

    [Fact]
    public async Task Quadro_DeCameraComEquipamentoMasFonteNavegador_Recusa404()
    {
        var c = await Preparar(nameof(Quadro_DeCameraComEquipamentoMasFonteNavegador_Recusa404));
        var lente = await c.Global.Cameras.SingleAsync(x => x.Id == c.Lente1.Id);
        lente.Fonte = FonteCamera.Navegador;
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => HandlerQuadro(c).Handle(Quadro(c, c.Lente1.Id), default));
    }

    [Fact]
    public async Task Quadro_DeCameraSemEquipamento_ModoArquivoLocal_ContinuaAceito()
    {
        var c = await Preparar(nameof(Quadro_DeCameraSemEquipamento_ModoArquivoLocal_ContinuaAceito));
        var local = new Camera { ObraId = c.ObraA.Id, Nome = "Câmera do arquivo local", Fonte = FonteCamera.Navegador };
        c.Global.Add(local);
        await c.Global.SaveChangesAsync();
        c.NovaRequisicao();

        var r = await HandlerQuadro(c).Handle(Quadro(c, local.Id), default);

        Assert.NotNull(r);
    }

    [Fact]
    public async Task Quadro_Atrasado_UsaOHorarioDaCaptura_ENuncaRecuaOUltimoQuadro()
    {
        var c = await Preparar(nameof(Quadro_Atrasado_UsaOHorarioDaCaptura_ENuncaRecuaOUltimoQuadro));
        var captura = DateTime.UtcNow.AddMinutes(-40);

        await HandlerQuadro(c).Handle(Quadro(c, c.Lente1.Id, captura), default);
        var depoisDoAtrasado = (await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.Lente1.Id)).UltimoQuadroRecebidoEmUtc;
        Assert.NotNull(depoisDoAtrasado);
        Assert.True((depoisDoAtrasado!.Value - captura).Duration() < TimeSpan.FromSeconds(1), "deveria guardar a hora da captura, não a do servidor");

        // Quadro ao vivo avança; um atrasado ainda mais antigo depois dele não faz o horário recuar.
        c.NovaRequisicao();
        await HandlerQuadro(c).Handle(Quadro(c, c.Lente1.Id), default);
        var aoVivo = (await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.Lente1.Id)).UltimoQuadroRecebidoEmUtc;
        Assert.True(DateTime.UtcNow - aoVivo < TimeSpan.FromMinutes(1));

        c.NovaRequisicao();
        await HandlerQuadro(c).Handle(Quadro(c, c.Lente1.Id, DateTime.UtcNow.AddMinutes(-30)), default);
        Assert.Equal(aoVivo, (await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.Lente1.Id)).UltimoQuadroRecebidoEmUtc);
    }

    private sealed class SenderQueApenasConfirma : MediatR.ISender
    {
        public int Chamadas { get; private set; }

        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult((TResponse)(object)new ResultadoQuadroDto(SituacaoQuadro.Limpo, null, false, 0, 0, 0));
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
