using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Câmera IP "quase ao vivo". SQLite em memória de verdade (e não o InMemory do EF) porque a gravação do quadro e o encerramento
// usam ExecuteUpdateAsync e porque o índice de uma sessão aberta por câmera precisa ser real. Três contextos na mesma base:
// global (semeia e confere), usuário restrito à obra A (a tela) e anônimo com escopo ZERADO (a rota do agente, como o
// EscopoPorObraMiddleware a deixa com o Entra ligado em produção/hml: se algo dependesse do filtro global, voltaria vazio).
public class SessaoAoVivoCameraTests : IDisposable
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20, 0x30 };
    private static readonly ISegredoDispositivoHasher Hasher = new SegredoDispositivoHasherService();
    private static readonly ICredencialCameraCriptografia Cripto = new CredencialCameraCriptografiaService();

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<SstDbContext> _opcoes;
    private readonly AoVivoCameraOpcoes _limites = new();

    private Obra _obraA = null!;
    private Obra _obraB = null!;
    private AgenteCamera _agenteA = null!;
    private AgenteCamera _agenteB = null!;
    private string _segredoA = null!;
    private string _segredoB = null!;
    private EquipamentoCamera _equipA = null!;
    private Camera _lente1 = null!;
    private Camera _lente2 = null!;
    private Camera _lente3 = null!;
    private Camera _cameraB = null!;

    public SessaoAoVivoCameraTests()
    {
        ChaveCredencialCameraDeTeste.Configurar();
        ChavesCpfDeTeste.Configurar();
        _conexao = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _conexao.Open();
        _conexao.CreateCollation("Latin1_General_100_BIN2", (a, b) => string.Compare(a, b, StringComparison.Ordinal));
        _opcoes = new DbContextOptionsBuilder<SstDbContext>().UseSqlite(_conexao).Options;
        using var db = new SstDbContext(_opcoes, new CurrentUserService());
        db.Database.EnsureCreated();
        Semear().GetAwaiter().GetResult();
    }

    public void Dispose() => _conexao.Dispose();

    private async Task Semear()
    {
        using var db = Global();
        _obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        _obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        _segredoA = Hasher.GerarSegredo();
        _segredoB = Hasher.GerarSegredo();
        _agenteA = new AgenteCamera { ObraId = _obraA.Id, Nome = "PC A", SegredoHash = Hasher.GerarHash(_segredoA), Versao = "1.1.0", UltimoContatoEmUtc = DateTime.UtcNow };
        _agenteB = new AgenteCamera { ObraId = _obraB.Id, Nome = "PC B", SegredoHash = Hasher.GerarHash(_segredoB), Versao = "1.1.0", UltimoContatoEmUtc = DateTime.UtcNow };
        _equipA = new EquipamentoCamera
        {
            ObraId = _obraA.Id, AgenteCameraId = _agenteA.Id, Nome = "A31", Host = "192.168.1.50", Usuario = "admin", SenhaCifrada = Cripto.Cifrar("senha-da-camera"),
        };
        Camera Ip(string nome, int canal) => new()
        {
            ObraId = _obraA.Id, Nome = nome, Fonte = FonteCamera.Rtsp, EquipamentoCameraId = _equipA.Id, Canal = canal, Stream = 1,
        };
        _lente1 = Ip("Lente 1", 1);
        _lente2 = Ip("Lente 2", 2);
        _lente3 = Ip("Lente 3", 3);
        _cameraB = new Camera { ObraId = _obraB.Id, Nome = "Câmera B", Fonte = FonteCamera.Rtsp };
        db.AddRange(_obraA, _obraB, _agenteA, _agenteB, _equipA, _lente1, _lente2, _lente3, _cameraB);
        await db.SaveChangesAsync();
    }

    private SstDbContext Global() => new(_opcoes, new CurrentUserService());

    private (SstDbContext Db, CurrentUserService Escopo) Usuario(params Guid[] obras)
    {
        var escopo = new CurrentUserService();
        escopo.DefinirEscopo(temAcessoGlobal: false, obras);
        return (new SstDbContext(_opcoes, escopo), escopo);
    }

    private (SstDbContext Db, CurrentUserService Escopo) Anonimo() => Usuario(Array.Empty<Guid>());

    private IOptions<AoVivoCameraOpcoes> Opcoes() => Options.Create(_limites);

    private async Task<IniciarSessaoAoVivoResultado> Iniciar(Guid cameraId, params Guid[] obras)
    {
        var (db, escopo) = Usuario(obras.Length == 0 ? new[] { _obraA.Id } : obras);
        using var _ = db;
        return await new IniciarSessaoAoVivoCameraCommandHandler(db, escopo, Opcoes()).Handle(new IniciarSessaoAoVivoCameraCommand(cameraId), default);
    }

    private async Task<ResultadoQuadroAoVivo> EnviarQuadro(Guid sessaoId, byte[]? jpeg = null, AgenteCamera? agente = null, string? segredo = null, DateTime? capturadoEm = null)
    {
        var (db, escopo) = Anonimo();
        using var _ = db;
        var handler = new RegistrarQuadroAoVivoAgenteCommandHandler(
            db, new AgenteCameraAutenticador(db, Hasher), escopo, NullLogger<RegistrarQuadroAoVivoAgenteCommandHandler>.Instance, Opcoes());
        return await handler.Handle(new RegistrarQuadroAoVivoAgenteCommand(
            (agente ?? _agenteA).Id, segredo ?? _segredoA, "1.1.0", sessaoId, jpeg ?? Jpeg, capturadoEm), default);
    }

    private async Task<bool> EnviarFalha(Guid sessaoId, string? codigo, AgenteCamera? agente = null, string? segredo = null)
    {
        var (db, escopo) = Anonimo();
        using var _ = db;
        var handler = new RegistrarFalhaAoVivoAgenteCommandHandler(
            db, new AgenteCameraAutenticador(db, Hasher), escopo, NullLogger<RegistrarFalhaAoVivoAgenteCommandHandler>.Instance);
        return await handler.Handle(new RegistrarFalhaAoVivoAgenteCommand((agente ?? _agenteA).Id, segredo ?? _segredoA, "1.1.0", sessaoId, codigo, "texto livre"), default);
    }

    private async Task<QuadroAoVivoDto> ObterQuadro(Guid sessaoId, long? apos = null, params Guid[] obras)
    {
        var (db, _) = Usuario(obras.Length == 0 ? new[] { _obraA.Id } : obras);
        using var _ = db;
        return await new ObterQuadroAoVivoQueryHandler(db, Opcoes()).Handle(new ObterQuadroAoVivoQuery(sessaoId, apos), default);
    }

    private async Task Encerrar(Guid sessaoId, params Guid[] obras)
    {
        var (db, _) = Usuario(obras.Length == 0 ? new[] { _obraA.Id } : obras);
        using var _ = db;
        await new EncerrarSessaoAoVivoCameraCommandHandler(db, Opcoes()).Handle(new EncerrarSessaoAoVivoCameraCommand(sessaoId), default);
    }

    private async Task<SessaoAoVivoCamera> Ler(Guid sessaoId)
    {
        using var db = Global();
        return await db.SessoesAoVivoCamera.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == sessaoId);
    }

    private async Task Alterar(Guid sessaoId, Action<SessaoAoVivoCamera> mudar)
    {
        using var db = Global();
        var s = await db.SessoesAoVivoCamera.IgnoreQueryFilters().SingleAsync(x => x.Id == sessaoId);
        mudar(s);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AbrirSessao(Camera? camera = null)
    {
        var r = await Iniciar((camera ?? _lente1).Id);
        Assert.NotNull(r.Sessao);
        return r.Sessao!.SessaoId;
    }

    // --- Abrir a sessão (tela) -----------------------------------------------------------------------------------

    [Fact]
    public async Task Iniciar_CameraDeOutraObra_NaoEncontrada()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Iniciar(_cameraB.Id)); // usuário restrito à obra A
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Iniciar(Guid.NewGuid()));
        using var db = Global();
        Assert.Empty(await db.SessoesAoVivoCamera.ToListAsync()); // nenhuma sessão foi criada
    }

    [Fact]
    public async Task Iniciar_CriaSessaoAguardando_ComFpsELimiteDoContrato()
    {
        var r = await Iniciar(_lente1.Id);

        Assert.Null(r.CodigoRecusa);
        Assert.Equal("Aguardando", r.Sessao!.Situacao);
        Assert.Equal(2, r.Sessao.Fps);
        Assert.InRange(r.Sessao.EncerraEmSegundos, 598, 600);
        var s = await Ler(r.Sessao.SessaoId);
        Assert.Equal(_obraA.Id, s.ObraId);
        Assert.Equal(_agenteA.Id, s.AgenteCameraId);
        Assert.Equal(_lente1.Id, s.CameraId);
        Assert.Null(s.IniciadaEmUtc);
        Assert.Null(s.EncerradaEmUtc);
    }

    [Fact]
    public async Task Iniciar_VariosEspectadores_ReaproveitamASessaoAberta()
    {
        var primeira = await AbrirSessao();
        var segunda = await Iniciar(_lente1.Id);

        Assert.Equal(primeira, segunda.Sessao!.SessaoId);
        using var db = Global();
        Assert.Equal(1, await db.SessoesAoVivoCamera.CountAsync());

        await EnviarQuadro(primeira);
        var terceira = await Iniciar(_lente1.Id);
        Assert.Equal(primeira, terceira.Sessao!.SessaoId);
        Assert.Equal("AoVivo", terceira.Sessao.Situacao);
    }

    [Fact]
    public async Task Iniciar_AposEncerrar_AbreSessaoNova_EMantemARegistroAnterior()
    {
        var antiga = await AbrirSessao();
        await Encerrar(antiga);
        await Alterar(antiga, s => s.UltimaVisualizacaoEmUtc = DateTime.UtcNow.AddSeconds(-61)); // passou o intervalo de ~10 s

        var nova = await AbrirSessao();

        Assert.NotEqual(antiga, nova);
        using var db = Global();
        Assert.Equal(2, await db.SessoesAoVivoCamera.CountAsync()); // trilha de acesso preservada
    }

    [Fact]
    public async Task Iniciar_NoMaximoUmaSessaoAbertaPorCamera_PeloIndiceDoBanco()
    {
        var id = await AbrirSessao();
        using var db = Global();
        var duplicada = new SessaoAoVivoCamera
        {
            ObraId = _obraA.Id, CameraId = _lente1.Id, AgenteCameraId = _agenteA.Id, SolicitadaEmUtc = DateTime.UtcNow,
            UltimaVisualizacaoEmUtc = DateTime.UtcNow, LimiteEmUtc = DateTime.UtcNow.AddMinutes(10), Fps = 2,
        };
        db.SessoesAoVivoCamera.Add(duplicada);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Iniciar_CameraDoNavegador_NaoECameraIp()
    {
        Guid id;
        using (var db = Global())
        {
            var c = new Camera { ObraId = _obraA.Id, Nome = "Tablet", Fonte = FonteCamera.Navegador };
            db.Cameras.Add(c);
            await db.SaveChangesAsync();
            id = c.Id;
        }
        Assert.Equal("nao_e_camera_ip", (await Iniciar(id)).CodigoRecusa);
    }

    [Fact]
    public async Task Iniciar_CameraDesabilitada_Recusa()
    {
        using (var db = Global())
        {
            (await db.Cameras.SingleAsync(c => c.Id == _lente1.Id)).Habilitada = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal("camera_desabilitada", (await Iniciar(_lente1.Id)).CodigoRecusa);
    }

    [Fact]
    public async Task Iniciar_AgenteRevogado_Recusa()
    {
        using (var db = Global())
        {
            (await db.AgentesCamera.SingleAsync(a => a.Id == _agenteA.Id)).Ativo = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal("agente_revogado", (await Iniciar(_lente1.Id)).CodigoRecusa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task Iniciar_AgenteSemContato_Recusa(int? minutosSemContato)
    {
        using (var db = Global())
        {
            (await db.AgentesCamera.SingleAsync(a => a.Id == _agenteA.Id)).UltimoContatoEmUtc =
                minutosSemContato is { } m ? DateTime.UtcNow.AddMinutes(-m) : null;
            await db.SaveChangesAsync();
        }
        Assert.Equal("agente_sem_contato", (await Iniciar(_lente1.Id)).CodigoRecusa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.0.9")]
    [InlineData("0.0.0")]
    public async Task Iniciar_AgenteDesatualizadoOuSemVersao_Recusa(string? versao)
    {
        using (var db = Global())
        {
            (await db.AgentesCamera.SingleAsync(a => a.Id == _agenteA.Id)).Versao = versao;
            await db.SaveChangesAsync();
        }
        Assert.Equal("agente_desatualizado", (await Iniciar(_lente1.Id)).CodigoRecusa);
    }

    [Theory]
    [InlineData("1.1.0", true)]
    [InlineData("1.1.4", true)]
    [InlineData("1.2", true)]
    [InlineData("2.0.0", true)]
    [InlineData("v1.1.0", true)]
    [InlineData("1.1.0-beta", true)]
    [InlineData("1.0.99", false)]
    [InlineData("1.1", false)] // 1.1 equivale a 1.1.-1: abaixo de 1.1.0
    [InlineData("lixo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AgenteSuportaAoVivo_ComparaComA110(string? versao, bool esperado)
        => Assert.Equal(esperado, SessaoAoVivoCameraRegras.AgenteSuportaAoVivo(versao));

    [Fact]
    public async Task Iniciar_LimiteDeDuasCamerasPorAgente_Recusa429NaTerceira()
    {
        await AbrirSessao(_lente1);
        await AbrirSessao(_lente2);

        var terceira = await Iniciar(_lente3.Id);

        Assert.Equal("limite_sessoes", terceira.CodigoRecusa);
        Assert.True(terceira.LimiteDeSessoes);
        // A câmera que já está ao vivo continua aceitando mais espectadores, mesmo no limite.
        Assert.Null((await Iniciar(_lente1.Id)).CodigoRecusa);
    }

    [Fact]
    public async Task Iniciar_AoEncerrarUma_LiberaVagaParaOutraCamera()
    {
        var a = await AbrirSessao(_lente1);
        await AbrirSessao(_lente2);
        await Alterar(a, s => { s.EncerradaEmUtc = DateTime.UtcNow; s.MotivoEncerramento = "encerrada"; });

        Assert.Null((await Iniciar(_lente3.Id)).CodigoRecusa);
    }

    [Fact]
    public async Task Iniciar_SessaoSemEspectadorHaMaisDe60s_EEncerradaEAbreNova()
    {
        var antiga = await AbrirSessao();
        await EnviarQuadro(antiga);
        await Alterar(antiga, s => s.UltimaVisualizacaoEmUtc = DateTime.UtcNow.AddSeconds(-61));

        var nova = await AbrirSessao();

        Assert.NotEqual(antiga, nova);
        var s = await Ler(antiga);
        Assert.Equal("inatividade", s.MotivoEncerramento);
        Assert.NotNull(s.EncerradaEmUtc);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task Iniciar_SessaoQuePassouDe10Minutos_EEncerradaComTempoMaximo()
    {
        var antiga = await AbrirSessao();
        await EnviarQuadro(antiga);
        await Alterar(antiga, s => s.LimiteEmUtc = DateTime.UtcNow.AddSeconds(-1));

        var nova = await AbrirSessao();

        Assert.NotEqual(antiga, nova);
        var s = await Ler(antiga);
        Assert.Equal("tempo_maximo", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    // --- Quadro do agente ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Quadro_Gravado_SobrescreveESobeASequencia_EIniciaASessao()
    {
        var id = await AbrirSessao();

        Assert.Equal(ResultadoQuadroAoVivo.Continuar, await EnviarQuadro(id, new byte[] { 0xFF, 0xD8, 1 }));
        var primeiro = await Ler(id);
        Assert.Equal(1, primeiro.UltimoQuadroSequencia);
        Assert.NotNull(primeiro.IniciadaEmUtc);

        Assert.Equal(ResultadoQuadroAoVivo.Continuar, await EnviarQuadro(id, new byte[] { 0xFF, 0xD8, 2, 2 }));
        var segundo = await Ler(id);
        Assert.Equal(2, segundo.UltimoQuadroSequencia);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 2, 2 }, segundo.UltimoQuadro); // só o último fica
        Assert.Equal(2, segundo.QuadrosRecebidos);
        Assert.Equal(7, segundo.BytesRecebidos);
        Assert.Equal(primeiro.IniciadaEmUtc, segundo.IniciadaEmUtc);
    }

    [Fact]
    public async Task Quadro_SemSegredoOuComSegredoErrado_NaoAutenticado_ENaoGrava()
    {
        var id = await AbrirSessao();

        Assert.Equal(ResultadoQuadroAoVivo.NaoAutenticado, await EnviarQuadro(id, segredo: "errado"));
        Assert.Equal(ResultadoQuadroAoVivo.NaoAutenticado, await EnviarQuadro(id, segredo: ""));
        Assert.Null((await Ler(id)).UltimoQuadro);
    }

    [Fact]
    public async Task Quadro_AgenteRevogado_NaoAutenticado()
    {
        var id = await AbrirSessao();
        using (var db = Global())
        {
            (await db.AgentesCamera.SingleAsync(a => a.Id == _agenteA.Id)).Ativo = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(ResultadoQuadroAoVivo.NaoAutenticado, await EnviarQuadro(id));
    }

    [Fact]
    public async Task Quadro_AgenteNaoEDonoDaSessao_SessaoNaoEncontrada_ENaoGrava()
    {
        var id = await AbrirSessao();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => EnviarQuadro(id, agente: _agenteB, segredo: _segredoB));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => EnviarQuadro(Guid.NewGuid()));
        Assert.Null((await Ler(id)).UltimoQuadro);
        Assert.Equal(0, (await Ler(id)).QuadrosRecebidos);
    }

    [Fact]
    public async Task Quadro_OutroAgenteDaMesmaObra_SessaoNaoEncontrada()
    {
        var id = await AbrirSessao();
        AgenteCamera irmao;
        string segredoIrmao;
        using (var db = Global())
        {
            segredoIrmao = Hasher.GerarSegredo();
            irmao = new AgenteCamera { ObraId = _obraA.Id, Nome = "PC A2", SegredoHash = Hasher.GerarHash(segredoIrmao), Versao = "1.1.0" };
            db.AgentesCamera.Add(irmao);
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => EnviarQuadro(id, agente: irmao, segredo: segredoIrmao));
    }

    [Fact]
    public async Task Quadro_SessaoEncerrada_Responde410_ENaoGrava()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await EnviarFalha(id, "sem_resposta"); // encerra de fato (o "fechei" da tela só antecipa a inatividade)

        Assert.Equal(ResultadoQuadroAoVivo.SessaoEncerrada, await EnviarQuadro(id));
        var s = await Ler(id);
        Assert.Null(s.UltimoQuadro);
        Assert.Equal(1, s.QuadrosRecebidos);
    }

    [Fact]
    public async Task Quadro_SessaoVencidaPeloRelogio_EEncerradaNaHora()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.LimiteEmUtc = DateTime.UtcNow.AddSeconds(-1));

        Assert.Equal(ResultadoQuadroAoVivo.SessaoEncerrada, await EnviarQuadro(id));
        var s = await Ler(id);
        Assert.Equal("tempo_maximo", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task Quadro_CameraDesabilitadaDepoisDeAbrir_EncerraEApagaOQuadro()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        using (var db = Global())
        {
            (await db.Cameras.SingleAsync(c => c.Id == _lente1.Id)).Habilitada = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(ResultadoQuadroAoVivo.SessaoEncerrada, await EnviarQuadro(id));
        var s = await Ler(id);
        Assert.Equal("camera_desabilitada", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task Quadro_AcimaDoTetoDaSessao_LimiteExcedido()
    {
        var id = await AbrirSessao();
        // fps 2 x 1,2 x 5 s de folga = 12 quadros de uma vez; o 13o passa do teto.
        for (var i = 0; i < 12; i++) Assert.Equal(ResultadoQuadroAoVivo.Continuar, await EnviarQuadro(id));
        Assert.Equal(ResultadoQuadroAoVivo.LimiteExcedido, await EnviarQuadro(id));
        Assert.Equal(12, (await Ler(id)).QuadrosRecebidos);
    }

    [Fact]
    public void Teto_AcompanhaOTempoDecorrido()
    {
        var agora = DateTime.UtcNow;
        // 60 s de transmissão a 2 q/s: 144 quadros no minuto (fps x 60 x 1,2) mais a folga de partida.
        Assert.False(SessaoAoVivoCameraRegras.ExcedeTetoDeQuadros(143, 2, agora.AddSeconds(-60), agora));
        Assert.True(SessaoAoVivoCameraRegras.ExcedeTetoDeQuadros(160, 2, agora.AddSeconds(-60), agora));
    }

    [Fact]
    public async Task Quadro_HorarioDeCapturaNoFuturoDistante_UsaOHorarioDoServidor()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id, capturadoEm: DateTime.UtcNow.AddHours(3));

        var capturado = (await Ler(id)).UltimoQuadroCapturadoEmUtc!.Value;
        Assert.True(capturado < DateTime.UtcNow.AddMinutes(1));
    }

    // --- Falha do agente -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Falha_EncerraComOCodigo_ETelaRecebe410ComFalhaCodigo()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);

        Assert.True(await EnviarFalha(id, "senha_recusada"));

        var s = await Ler(id);
        Assert.Equal("falha", s.MotivoEncerramento);
        Assert.Equal("senha_recusada", s.CodigoFalha);
        Assert.Null(s.UltimoQuadro);
        var quadro = await ObterQuadro(id);
        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, quadro.Situacao);
        Assert.Equal("falha:senha_recusada", quadro.Motivo);
    }

    [Fact]
    public async Task Falha_CodigoDesconhecido_ViraErroDesconhecido()
    {
        var id = await AbrirSessao();
        await EnviarFalha(id, "inventado<script>");
        Assert.Equal("erro_desconhecido", (await Ler(id)).CodigoFalha);
    }

    [Fact]
    public async Task Falha_OutroAgenteOuSemSegredo_NaoEncerra()
    {
        var id = await AbrirSessao();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => EnviarFalha(id, "sem_resposta", _agenteB, _segredoB));
        Assert.False(await EnviarFalha(id, "sem_resposta", segredo: "errado"));
        Assert.Null((await Ler(id)).EncerradaEmUtc);
    }

    // --- Quadro para a tela --------------------------------------------------------------------------------------

    [Fact]
    public async Task ObterQuadro_AntesDoPrimeiro_Aguardando()
    {
        var id = await AbrirSessao();
        Assert.Equal(SituacaoQuadroAoVivo.Aguardando, (await ObterQuadro(id)).Situacao);
    }

    [Fact]
    public async Task ObterQuadro_EntregaOQuadro_ESoOQueEMaisNovo()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id, new byte[] { 0xFF, 0xD8, 9, 9 });

        var primeiro = await ObterQuadro(id);
        Assert.Equal(SituacaoQuadroAoVivo.Quadro, primeiro.Situacao);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 9, 9 }, primeiro.Jpeg);
        Assert.Equal(1, primeiro.Sequencia);
        Assert.NotNull(primeiro.CapturadoEmUtc);

        Assert.Equal(SituacaoQuadroAoVivo.SemNovidade, (await ObterQuadro(id, apos: 1)).Situacao);
        await EnviarQuadro(id);
        var segundo = await ObterQuadro(id, apos: 1);
        Assert.Equal(SituacaoQuadroAoVivo.Quadro, segundo.Situacao);
        Assert.Equal(2, segundo.Sequencia);
    }

    [Fact]
    public async Task ObterQuadro_UsuarioDeOutraObra_NaoEncontrada()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ObterQuadro(id, null, _obraB.Id));
    }

    [Fact]
    public async Task ObterQuadro_MarcaEspectador_NoMaximoACada5s()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);

        var recente = DateTime.UtcNow.AddSeconds(-3);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = recente);
        await ObterQuadro(id);
        Assert.Equal(recente, (await Ler(id)).UltimaVisualizacaoEmUtc, TimeSpan.FromMilliseconds(1)); // < 5 s: não regravou

        var antiga = DateTime.UtcNow.AddSeconds(-10);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = antiga);
        await ObterQuadro(id);
        Assert.True((await Ler(id)).UltimaVisualizacaoEmUtc > antiga.AddSeconds(5));
    }

    [Fact]
    public async Task ObterQuadro_SemEspectadorHaMaisDe60s_Inatividade_EApagaOQuadro()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = DateTime.UtcNow.AddSeconds(-61));

        var r = await ObterQuadro(id);

        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, r.Situacao);
        Assert.Equal("inatividade", r.Motivo);
        Assert.Null(r.Jpeg);
        Assert.Null((await Ler(id)).UltimoQuadro);
    }

    [Fact]
    public async Task ObterQuadro_PassouDoTempoMaximo_TempoMaximo()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Alterar(id, s => s.LimiteEmUtc = DateTime.UtcNow.AddSeconds(-1));

        var r = await ObterQuadro(id);

        Assert.Equal("tempo_maximo", r.Motivo);
        Assert.Null((await Ler(id)).UltimoQuadro);
    }

    [Fact]
    public async Task ObterQuadro_AgenteNaoComecouEm60s_AgenteSemResposta()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-61));

        var r = await ObterQuadro(id);

        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, r.Situacao);
        Assert.Equal("agente_sem_resposta", r.Motivo);
    }

    [Fact]
    public async Task Quadro_GravaOHorarioDoServidorEmQueChegou()
    {
        var id = await AbrirSessao();
        var antes = DateTime.UtcNow;

        await EnviarQuadro(id, capturadoEm: DateTime.UtcNow.AddHours(-3)); // relógio do PC da obra errado

        var s = await Ler(id);
        Assert.NotNull(s.UltimoQuadroRecebidoEmUtc);
        Assert.InRange(s.UltimoQuadroRecebidoEmUtc!.Value, antes.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.True(s.UltimoQuadroCapturadoEmUtc < antes.AddHours(-2));
    }

    [Fact]
    public async Task ObterQuadro_IniciadaSemQuadroHaMaisDe30s_AgenteSemResposta_EApagaOQuadro()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Alterar(id, s => s.UltimoQuadroRecebidoEmUtc = DateTime.UtcNow.AddSeconds(-31));

        var r = await ObterQuadro(id);

        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, r.Situacao);
        Assert.Equal("agente_sem_resposta", r.Motivo);
        Assert.Null(r.Jpeg);
        var s = await Ler(id);
        Assert.Equal("agente_sem_resposta", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task ObterQuadro_ComQuadroRecente_SegueAoVivo_MesmoComRelogioDaObraErrado()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id, capturadoEm: DateTime.UtcNow.AddHours(-3));
        await Alterar(id, s => s.UltimoQuadroRecebidoEmUtc = DateTime.UtcNow.AddSeconds(-5));

        var r = await ObterQuadro(id);

        Assert.Equal(SituacaoQuadroAoVivo.Quadro, r.Situacao);
        Assert.Null((await Ler(id)).EncerradaEmUtc);
    }

    [Fact]
    public async Task ObterQuadro_SessaoAguardando_ContinuaNaRegraDos60s_NaoDos30s()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-45));

        var r = await ObterQuadro(id);

        Assert.Equal(SituacaoQuadroAoVivo.Aguardando, r.Situacao);
        Assert.Null((await Ler(id)).EncerradaEmUtc);
    }

    [Fact]
    public async Task Quadro_AgenteVoltaDepoisDeMaisDe30s_SessaoEncerradaENaoGrava()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Alterar(id, s => s.UltimoQuadroRecebidoEmUtc = DateTime.UtcNow.AddSeconds(-31));

        Assert.Equal(ResultadoQuadroAoVivo.SessaoEncerrada, await EnviarQuadro(id));

        var s = await Ler(id);
        Assert.Equal("agente_sem_resposta", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task Limpeza_IniciadaSemQuadroHaMaisDe30s_Encerra_ComQuadroRecenteNaoMexe()
    {
        var semQuadro = await AbrirSessao(_lente1);
        var comQuadro = await AbrirSessao(_lente2);
        await EnviarQuadro(semQuadro);
        await EnviarQuadro(comQuadro);
        await Alterar(semQuadro, s => s.UltimoQuadroRecebidoEmUtc = DateTime.UtcNow.AddSeconds(-31));

        using var db = Global();
        var encerradas = await new LimparSessoesAoVivoCameraParadasCommandHandler(db, Opcoes()).Handle(new LimparSessoesAoVivoCameraParadasCommand(), default);

        Assert.Equal(1, encerradas);
        var a = await Ler(semQuadro);
        Assert.Equal("agente_sem_resposta", a.MotivoEncerramento);
        Assert.Null(a.UltimoQuadro);
        Assert.Null((await Ler(comQuadro)).EncerradaEmUtc);
    }

    [Fact]
    public async Task Limpeza_SessaoAguardandoHa45s_NaoEncerra()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-45));

        using var db = Global();
        var encerradas = await new LimparSessoesAoVivoCameraParadasCommandHandler(db, Opcoes()).Handle(new LimparSessoesAoVivoCameraParadasCommand(), default);

        Assert.Equal(0, encerradas);
        Assert.Null((await Ler(id)).EncerradaEmUtc);
    }

    [Fact]
    public async Task Limpeza_AguardandoHaMaisDe60s_ContinuaEncerrandoComAgenteSemResposta()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-61));

        using var db = Global();
        await new LimparSessoesAoVivoCameraParadasCommandHandler(db, Opcoes()).Handle(new LimparSessoesAoVivoCameraParadasCommand(), default);

        Assert.Equal("agente_sem_resposta", (await Ler(id)).MotivoEncerramento);
    }

    [Fact]
    public async Task Encerrar_NaoEncerraNaHora_AntecipaAInatividadeParaCercaDe10s()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);

        await Encerrar(id);

        var s = await Ler(id);
        Assert.Null(s.EncerradaEmUtc); // outro espectador pode estar olhando
        Assert.NotNull(s.UltimoQuadro);
        var segundosParaInatividade = _limites.InatividadeSegundos - (DateTime.UtcNow - s.UltimaVisualizacaoEmUtc).TotalSeconds;
        Assert.InRange(segundosParaInatividade, 8, 10.5);
    }

    [Fact]
    public async Task Encerrar_ComUmSoEspectador_EncerraPorInatividadeEm10s_ApagaOQuadro_MasGuardaOHistorico()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);

        await Encerrar(id);
        // Passam ~11 s sem ninguém buscar quadro: simula recuando o horário gravado (a tela fechada não busca mais).
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = s.UltimaVisualizacaoEmUtc.AddSeconds(-11));
        var r = await ObterQuadro(id); // a leitura (ou a limpeza do Worker) é quem encerra

        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, r.Situacao);
        Assert.Equal("inatividade", r.Motivo);
        var s = await Ler(id);
        Assert.Null(s.UltimoQuadro);
        Assert.NotNull(s.EncerradaEmUtc);
        Assert.Equal("inatividade", s.MotivoEncerramento);
        Assert.Equal(_lente1.Id, s.CameraId);
        Assert.Equal(1, s.QuadrosRecebidos);
    }

    [Fact]
    public async Task Encerrar_ComUmSoEspectador_AsParadasDaLimpezaEncerramPorInatividadeEm10s()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Encerrar(id);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = s.UltimaVisualizacaoEmUtc.AddSeconds(-11));

        using (var db = Global())
            Assert.Equal(1, await SessaoAoVivoCameraRegras.EncerrarParadasAsync(db, _limites, null, DateTime.UtcNow, default));

        Assert.Equal("inatividade", (await Ler(id)).MotivoEncerramento);
    }

    [Fact]
    public async Task Encerrar_ComOutroEspectadorAtivo_NaoEncerra_ASessaoSegue()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        await Encerrar(id); // um espectador fechou

        // O outro busca quadro dentro do intervalo: o horário gravado é bem mais velho que 5 s, então a marcação regrava "agora".
        var r = await ObterQuadro(id);
        Assert.Equal(SituacaoQuadroAoVivo.Quadro, r.Situacao);
        var s = await Ler(id);
        Assert.True((DateTime.UtcNow - s.UltimaVisualizacaoEmUtc).TotalSeconds < 2);
        Assert.Null(s.EncerradaEmUtc);

        // Passados os 10 s desde o "fechei", a sessão segue viva porque o horário foi renovado.
        Assert.Equal(SituacaoQuadroAoVivo.SemNovidade, (await ObterQuadro(id, r.Sequencia)).Situacao);
        var depois = await Ler(id);
        Assert.Null(depois.EncerradaEmUtc);
        Assert.NotNull(depois.UltimoQuadro);
    }

    [Fact]
    public async Task Encerrar_NaoAdiaUmaSessaoQueJaEstavaParaEncerrar()
    {
        var id = await AbrirSessao();
        var antiga = DateTime.UtcNow.AddSeconds(-55);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = antiga);

        await Encerrar(id);

        Assert.Equal(antiga, (await Ler(id)).UltimaVisualizacaoEmUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Encerrar_SessaoJaEncerrada_NaoFazNada_ENaoMudaOMotivo()
    {
        var id = await AbrirSessao();
        await EnviarFalha(id, "sem_resposta");
        var antes = await Ler(id);

        await Encerrar(id);

        var depois = await Ler(id);
        Assert.Equal("falha", depois.MotivoEncerramento);
        Assert.Equal(antes.EncerradaEmUtc, depois.EncerradaEmUtc);
        Assert.Equal(antes.UltimaVisualizacaoEmUtc, depois.UltimaVisualizacaoEmUtc);
    }

    [Fact]
    public async Task Encerrar_SessaoDeOutraObra_NaoEncontrada_ENaoMexeNaSessao()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        var antes = await Ler(id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Encerrar(id, _obraB.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Encerrar(Guid.NewGuid()));

        var depois = await Ler(id);
        Assert.Null(depois.EncerradaEmUtc);
        Assert.Equal(antes.UltimaVisualizacaoEmUtc, depois.UltimaVisualizacaoEmUtc); // o "fechei" de outra obra não antecipou nada
        Assert.NotNull(depois.UltimoQuadro);
    }

    [Fact]
    public async Task ObterQuadro_UsuarioDeOutraObra_NaoEncontrada_ENaoMarcaEspectadorNemEncerra()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        var velha = DateTime.UtcNow.AddSeconds(-30);
        await Alterar(id, s => s.UltimaVisualizacaoEmUtc = velha);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => ObterQuadro(id, null, _obraB.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ObterQuadro(id, 0, _obraB.Id));

        var s = await Ler(id);
        Assert.Equal(velha, s.UltimaVisualizacaoEmUtc, TimeSpan.FromMilliseconds(1));
        Assert.Null(s.EncerradaEmUtc);
        Assert.NotNull(s.UltimoQuadro);
    }

    // --- Configuração do agente ----------------------------------------------------------------------------------

    private async Task<ConfiguracaoAgenteCameraDto> Configuracao()
    {
        var (db, escopo) = Anonimo();
        using var _ = db;
        var handler = new ObterConfiguracaoAgenteCameraQueryHandler(
            db, new AgenteCameraAutenticador(db, Hasher), escopo, Cripto, NullLogger<ObterConfiguracaoAgenteCameraQueryHandler>.Instance, Opcoes());
        return (await handler.Handle(new ObterConfiguracaoAgenteCameraQuery(_agenteA.Id, _segredoA, "1.1.0"), default))!;
    }

    [Fact]
    public async Task Configuracao_ListaSessaoAberta_ComTempoRelativoENuncaNegativo_ComEscopoZerado()
    {
        var id = await AbrirSessao();

        var config = await Configuracao();

        var item = Assert.Single(config.AoVivo!);
        Assert.Equal(id, item.SessaoId);
        Assert.Equal(_lente1.Id, item.CameraId);
        Assert.Equal(2, item.Fps);
        Assert.Equal(640, item.LarguraMaxima);
        Assert.InRange(item.SegundosRestantes, 598, 600);
        Assert.Contains(config.Cameras, c => c.CameraId == _lente1.Id);
    }

    [Fact]
    public async Task Configuracao_MaisSessoesQueOLimite_EntregaAsMaisAntigas_EEncerraAsExcedentesComLimiteSessoes()
    {
        // Cliques simultâneos furaram a contagem do Iniciar: três sessões abertas para um agente com limite de 2
        // (a terceira é inserida direto, como a corrida a deixaria).
        var s1 = await AbrirSessao(_lente1);
        var s2 = await AbrirSessao(_lente2);
        Guid s3;
        using (var db = Global())
        {
            var nova = new SessaoAoVivoCamera
            {
                ObraId = _obraA.Id, CameraId = _lente3.Id, AgenteCameraId = _agenteA.Id, SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-10),
                UltimaVisualizacaoEmUtc = DateTime.UtcNow, LimiteEmUtc = DateTime.UtcNow.AddMinutes(10), Fps = 2,
            };
            db.SessoesAoVivoCamera.Add(nova);
            await db.SaveChangesAsync();
            s3 = nova.Id;
        }
        await Alterar(s1, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-30));
        await Alterar(s2, s => s.SolicitadaEmUtc = DateTime.UtcNow.AddSeconds(-20));

        var config = await Configuracao();

        Assert.Equal(new[] { s1, s2 }, config.AoVivo!.Select(x => x.SessaoId).ToArray());
        var excedente = await Ler(s3);
        Assert.NotNull(excedente.EncerradaEmUtc);
        Assert.Equal("limite_sessoes", excedente.MotivoEncerramento);
        Assert.Null(excedente.UltimoQuadro);
        Assert.Null((await Ler(s1)).EncerradaEmUtc);
        Assert.Null((await Ler(s2)).EncerradaEmUtc);

        // A tela da sessão cortada recebe 410 com o motivo limite_sessoes.
        var tela = await ObterQuadro(s3);
        Assert.Equal(SituacaoQuadroAoVivo.Encerrada, tela.Situacao);
        Assert.Equal("limite_sessoes", tela.Motivo);
    }

    [Fact]
    public async Task Configuracao_DentroDoLimite_NaoEncerraNada()
    {
        var s1 = await AbrirSessao(_lente1);
        var s2 = await AbrirSessao(_lente2);

        var config = await Configuracao();

        Assert.Equal(2, config.AoVivo!.Count);
        Assert.Null((await Ler(s1)).EncerradaEmUtc);
        Assert.Null((await Ler(s2)).EncerradaEmUtc);
    }

    [Fact]
    public async Task Configuracao_SemSessao_ListaVazia()
        => Assert.Empty((await Configuracao()).AoVivo!);

    [Fact]
    public async Task Configuracao_SessaoEncerrada_NaoEntra()
    {
        var id = await AbrirSessao();
        await Alterar(id, x => { x.EncerradaEmUtc = DateTime.UtcNow; x.MotivoEncerramento = "encerrada"; });
        Assert.Empty((await Configuracao()).AoVivo!);
    }

    [Fact]
    public async Task Configuracao_CameraDesabilitada_EncerraAsessaoENaoLista()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        using (var db = Global())
        {
            (await db.Cameras.SingleAsync(c => c.Id == _lente1.Id)).Habilitada = false;
            await db.SaveChangesAsync();
        }

        var config = await Configuracao();

        Assert.Empty(config.AoVivo!);
        var s = await Ler(id);
        Assert.Equal("camera_desabilitada", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public async Task Configuracao_SessaoVencida_EncerraENaoLista()
    {
        var id = await AbrirSessao();
        await Alterar(id, s => s.LimiteEmUtc = DateTime.UtcNow.AddSeconds(-5));

        Assert.Empty((await Configuracao()).AoVivo!);
        Assert.Equal("tempo_maximo", (await Ler(id)).MotivoEncerramento);
    }

    [Fact]
    public async Task Configuracao_SoListaSessoesDoProprioAgente()
    {
        await AbrirSessao();
        Guid outraSessao;
        using (var db = Global())
        {
            var s = new SessaoAoVivoCamera
            {
                ObraId = _obraB.Id, CameraId = _cameraB.Id, AgenteCameraId = _agenteB.Id, SolicitadaEmUtc = DateTime.UtcNow,
                UltimaVisualizacaoEmUtc = DateTime.UtcNow, LimiteEmUtc = DateTime.UtcNow.AddMinutes(5), Fps = 2,
            };
            db.SessoesAoVivoCamera.Add(s);
            await db.SaveChangesAsync();
            outraSessao = s.Id;
        }

        var config = await Configuracao();

        Assert.DoesNotContain(config.AoVivo!, a => a.SessaoId == outraSessao);
        Assert.Single(config.AoVivo!);
    }

    // --- Limpeza (Worker) ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Limpeza_EncerraAsParadas_EApagaOsQuadros_SemTocarNasVivas()
    {
        var parada = await AbrirSessao(_lente1);
        var viva = await AbrirSessao(_lente2);
        await EnviarQuadro(parada);
        await EnviarQuadro(viva);
        await Alterar(parada, s => s.UltimaVisualizacaoEmUtc = DateTime.UtcNow.AddMinutes(-5));

        using var db = Global();
        var encerradas = await new LimparSessoesAoVivoCameraParadasCommandHandler(db, Opcoes()).Handle(new LimparSessoesAoVivoCameraParadasCommand(), default);

        Assert.Equal(1, encerradas);
        var p = await Ler(parada);
        Assert.Equal("inatividade", p.MotivoEncerramento);
        Assert.Null(p.UltimoQuadro);
        var v = await Ler(viva);
        Assert.Null(v.EncerradaEmUtc);
        Assert.NotNull(v.UltimoQuadro);
    }

    [Fact]
    public async Task Limpeza_AgenteRevogado_EncerraAsSessoesDele()
    {
        var id = await AbrirSessao();
        await EnviarQuadro(id);
        using (var db = Global())
        {
            (await db.AgentesCamera.SingleAsync(a => a.Id == _agenteA.Id)).Ativo = false;
            await db.SaveChangesAsync();
        }

        using var limpeza = Global();
        await new LimparSessoesAoVivoCameraParadasCommandHandler(limpeza, Opcoes()).Handle(new LimparSessoesAoVivoCameraParadasCommand(), default);

        var s = await Ler(id);
        Assert.Equal("agente_revogado", s.MotivoEncerramento);
        Assert.Null(s.UltimoQuadro);
    }

    [Fact]
    public void SegundosRestantes_NuncaNegativo()
    {
        var agora = DateTime.UtcNow;
        Assert.Equal(0, SessaoAoVivoCameraRegras.SegundosRestantes(agora.AddSeconds(-30), agora));
        Assert.Equal(0, SessaoAoVivoCameraRegras.SegundosRestantes(agora, agora));
        Assert.Equal(11, SessaoAoVivoCameraRegras.SegundosRestantes(agora.AddSeconds(10.2), agora));
    }

    [Fact]
    public void MotivoParaTela_CameraDesligadaEAgenteRevogadoViramEncerrada()
    {
        Assert.Equal("encerrada", SessaoAoVivoCameraRegras.MotivoParaTela("camera_desabilitada", null));
        Assert.Equal("encerrada", SessaoAoVivoCameraRegras.MotivoParaTela("agente_revogado", null));
        Assert.Equal("falha:erro_desconhecido", SessaoAoVivoCameraRegras.MotivoParaTela("falha", null));
    }

    [Fact]
    public void QuadroAoVivoDto_ToStringNaoLevaImagem()
    {
        var texto = new QuadroAoVivoDto(SituacaoQuadroAoVivo.Quadro, new byte[] { 0xFF, 0xD8, 1, 2, 3 }, 7, null, null).ToString();
        Assert.DoesNotContain("255", texto);
        Assert.Contains("Bytes = 5", texto);
    }
}
