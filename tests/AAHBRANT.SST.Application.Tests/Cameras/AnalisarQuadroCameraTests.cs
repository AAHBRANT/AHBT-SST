using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Cameras;

public class AnalisarQuadroCameraTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20 };

    private sealed class AnalisadorFalso : IAnalisadorEpiImagem
    {
        private readonly Func<ResultadoAnaliseEpi> _resposta;
        public int Chamadas { get; private set; }
        public AnalisadorFalso(Func<ResultadoAnaliseEpi> resposta) => _resposta = resposta;
        public Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(_resposta());
        }
    }

    private sealed class TelegramFalso : ITelegramResumoService
    {
        public bool Resultado { get; set; } = true;
        public List<string> Legendas { get; } = new();
        public Task EnviarAsync(string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default)
        {
            Legendas.Add(legenda);
            return Task.FromResult(Resultado);
        }
    }

    // O provedor InMemory não aplica rowversion: simula o conflito quando a câmera tem a propriedade alterada.
    private sealed class ConflitoNaCameraInterceptor : SaveChangesInterceptor
    {
        private readonly string _propriedade;
        public bool Armado { get; set; }
        public ConflitoNaCameraInterceptor(string propriedade) => _propriedade = propriedade;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armado && eventData.Context!.ChangeTracker.Entries<Camera>()
                    .Any(e => e.State == EntityState.Modified && e.Property(_propriedade).IsModified))
                throw new DbUpdateConcurrencyException("Conflito simulado na câmera.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static async Task<(SstDbContext db, Camera camera)> Preparar(string nomeBanco, Action<Camera>? ajustar = null,
        IInterceptor? interceptor = null)
    {
        var db = CamerasPersistenciaTests.CriarDb(nomeBanco, interceptor);
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        var camera = new Camera { ObraId = obra.Id, Nome = "Betoneira" };
        ajustar?.Invoke(camera);
        db.AddRange(obra, camera);
        await db.SaveChangesAsync();
        return (db, camera);
    }

    private static AnalisarQuadroCameraCommandHandler Criar(SstDbContext db, IAnalisadorEpiImagem analisador, ITelegramResumoService telegram,
        IAutenticacaoFacialService? facial = null)
        => new(db, analisador, telegram, facial ?? new FacialFalso(NaoReconhecido), NullLogger<AnalisarQuadroCameraCommandHandler>.Instance);

    private static ResultadoIdentificacaoFacial NaoReconhecido() => new(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

    private static ResultadoIdentificacaoFacial Reconhecido(Guid trabalhadorId)
        => new(true, new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.ReconhecimentoFacial, Confianca: 91), null, 0.91);

    // Só a identificação sem registro de falha interessa às câmeras; o caminho da assinatura não pode ser usado.
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        private readonly Func<ResultadoIdentificacaoFacial> _resposta;
        public int Chamadas { get; private set; }
        public Guid? ObraConsultada { get; private set; }
        public FacialFalso(Func<ResultadoIdentificacaoFacial> resposta) => _resposta = resposta;

        public Task<ResultadoIdentificacaoFacial> IdentificarSemRegistrarFalhaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        {
            Chamadas++;
            ObraConsultada = obraId;
            return Task.FromResult(_resposta());
        }

        // Cena com várias pessoas: rostos devolvidos pelo reconhecimento (posição + trabalhador).
        public Func<IReadOnlyList<RostoNaCena>> RostosDaCena { get; set; } = () => Array.Empty<RostoNaCena>();
        public int ChamadasCena { get; private set; }
        public Task<IReadOnlyList<RostoNaCena>> IdentificarRostosDaCenaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        {
            ChamadasCena++;
            return Task.FromResult(RostosDaCena());
        }

        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
            => throw new NotSupportedException("A câmera não pode registrar falha de cadastro fraco.");
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => throw new NotSupportedException();
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
    }

    private static ResultadoAnaliseEpi UmaPessoaSemCapacete() => new(1, 1, 0, ConfiancaAnaliseEpi.Alta);

    [Fact]
    public async Task UmaPessoaReconhecidaPeloRosto_VinculaTrabalhadorECitaNomeNoAlerta()
    {
        var (db, camera) = await Preparar(nameof(UmaPessoaReconhecidaPeloRosto_VinculaTrabalhadorECitaNomeNoAlerta));
        var trabalhador = new Trabalhador { ObraId = camera.ObraId, Nome = "João da Silva" };
        db.Add(trabalhador);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(() => Reconhecido(trabalhador.Id));

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.Equal(trabalhador.Id, r.TrabalhadorId);
        Assert.Equal("João da Silva", r.TrabalhadorNome);
        Assert.Equal(camera.ObraId, facial.ObraConsultada);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(trabalhador.Id, ocorrencia.TrabalhadorId);
        Assert.True(ocorrencia.IdentificadoPorRosto);
        Assert.Equal(91, ocorrencia.ConfiancaIdentificacao);
        Assert.NotNull(ocorrencia.VinculadoEmUtc);
        Assert.Null(ocorrencia.VinculadoPorUsuarioId);
        // Sem conferência humana, a ocorrência continua "nova".
        Assert.Equal(SituacaoOcorrenciaEpi.Nova, ocorrencia.Situacao);
        Assert.EndsWith("Trabalhador: João da Silva (identificado pelo rosto)", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task MaisDeUmaPessoaNaCena_NaoTentaIdentificarPeloRosto()
    {
        var (db, camera) = await Preparar(nameof(MaisDeUmaPessoaNaCena_NaoTentaIdentificarPeloRosto));
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(0, facial.Chamadas);
        Assert.Null(r.TrabalhadorId);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Null(ocorrencia.TrabalhadorId);
        Assert.False(ocorrencia.IdentificadoPorRosto);
        Assert.EndsWith("Trabalhador não identificado", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task RostoNaoReconhecido_GravaOcorrenciaSemTrabalhadorEAlertaComoNaoIdentificado()
    {
        var (db, camera) = await Preparar(nameof(RostoNaoReconhecido_GravaOcorrenciaSemTrabalhadorEAlertaComoNaoIdentificado));
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido);

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(1, facial.Chamadas);
        Assert.True(r.AlertaEnviado);
        Assert.Null(r.TrabalhadorNome);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Null(ocorrencia.TrabalhadorId);
        Assert.False(ocorrencia.IdentificadoPorRosto);
        Assert.EndsWith("Trabalhador não identificado", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task FacialIndisponivel_OcorrenciaEAlertaSaemSemTrabalhador()
    {
        var (db, camera) = await Preparar(nameof(FacialIndisponivel_OcorrenciaEAlertaSaemSemTrabalhador));
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(() => throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra."));

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Null(ocorrencia.TrabalhadorId);
        Assert.False(ocorrencia.IdentificadoPorRosto);
        Assert.Single(telegram.Legendas);
    }

    [Fact]
    public async Task CenaRegularComUmaPessoa_NaoChamaReconhecimentoFacial()
    {
        var (db, camera) = await Preparar(nameof(CenaRegularComUmaPessoa_NaoChamaReconhecimentoFacial));
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        await Criar(db, new AnalisadorFalso(() => new ResultadoAnaliseEpi(1, 0, 0, ConfiancaAnaliseEpi.Alta)), new TelegramFalso(), facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(0, facial.Chamadas);
    }

    // Duas pessoas afastadas: a da esquerda sem capacete, a da direita sem colete.
    private static ResultadoAnaliseEpi DuasPessoasIrregulares() => new(2, 1, 1, ConfiancaAnaliseEpi.Alta, new[]
    {
        new PessoaNaCena(0.25, 0.30, true, false),
        new PessoaNaCena(0.75, 0.30, false, true),
    });

    private static RostoNaCena RostoEm(double cx, double cy, Guid? trabalhadorId)
        => new(cx - 0.03, cy - 0.05, 0.06, 0.10, trabalhadorId, trabalhadorId is null ? null : 92);

    [Fact]
    public async Task VariasPessoas_IdentificaCadaIrregularEEnviaUmAlertaComOsNomes()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_IdentificaCadaIrregularEEnviaUmAlertaComOsNomes));
        var joao = new Trabalhador { ObraId = camera.ObraId, Nome = "João da Silva" };
        var pedro = new Trabalhador { ObraId = camera.ObraId, Nome = "Pedro Souza" };
        db.AddRange(joao, pedro);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido)
        {
            RostosDaCena = () => new[] { RostoEm(0.75, 0.31, pedro.Id), RostoEm(0.25, 0.31, joao.Id) },
        };

        var r = await Criar(db, new AnalisadorFalso(DuasPessoasIrregulares), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Equal("João da Silva, Pedro Souza", r.TrabalhadorNome);
        Assert.Equal(0, facial.Chamadas);
        Assert.Equal(1, facial.ChamadasCena);

        var ocorrencias = await db.OcorrenciasEpiCamera.ToListAsync();
        Assert.Equal(2, ocorrencias.Count);
        var doJoao = ocorrencias.Single(o => o.TrabalhadorId == joao.Id);
        Assert.Equal((1, 0), (doJoao.SemCapacete, doJoao.SemColete));
        Assert.True(doJoao.IdentificadoPorRosto);
        Assert.True(doJoao.AlertaEnviado);
        var doPedro = ocorrencias.Single(o => o.TrabalhadorId == pedro.Id);
        Assert.Equal((0, 1), (doPedro.SemCapacete, doPedro.SemColete));
        Assert.All(ocorrencias, o => Assert.Equal(2, o.PessoasNaCena));

        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("2 pessoas na cena, 1 sem capacete, 1 sem colete", legenda);
        Assert.EndsWith("Identificados pelo rosto: João da Silva (sem capacete), Pedro Souza (sem colete)", legenda);
    }

    [Fact]
    public async Task VariasPessoas_SoUmReconhecido_OutroFicaNumaOcorrenciaSemTrabalhador()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_SoUmReconhecido_OutroFicaNumaOcorrenciaSemTrabalhador));
        var joao = new Trabalhador { ObraId = camera.ObraId, Nome = "João da Silva" };
        db.Add(joao);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido)
        {
            RostosDaCena = () => new[] { RostoEm(0.25, 0.31, joao.Id), RostoEm(0.75, 0.31, null) },
        };

        var r = await Criar(db, new AnalisadorFalso(DuasPessoasIrregulares), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(joao.Id, r.TrabalhadorId);
        var ocorrencias = await db.OcorrenciasEpiCamera.ToListAsync();
        Assert.Equal(2, ocorrencias.Count);
        var semNome = ocorrencias.Single(o => o.TrabalhadorId == null);
        Assert.Equal((0, 1), (semNome.SemCapacete, semNome.SemColete));
        Assert.False(semNome.IdentificadoPorRosto);
        Assert.True(semNome.AlertaEnviado);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("Identificados pelo rosto: João da Silva (sem capacete)", legenda);
        Assert.EndsWith("Não identificados: 1", legenda);
    }

    [Fact]
    public async Task VariasPessoas_QuemFoiAlertadoNaUltimaHoraFicaForaDoAlerta()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_QuemFoiAlertadoNaUltimaHoraFicaForaDoAlerta));
        var joao = await ComAlertaAnterior(db, camera, minutosAtras: 15);
        var pedro = new Trabalhador { ObraId = camera.ObraId, Nome = "Pedro Souza" };
        db.Add(pedro);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido)
        {
            RostosDaCena = () => new[] { RostoEm(0.25, 0.31, joao.Id), RostoEm(0.75, 0.31, pedro.Id) },
        };

        await Criar(db, new AnalisadorFalso(DuasPessoasIrregulares), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        var legenda = Assert.Single(telegram.Legendas);
        Assert.DoesNotContain("João da Silva", legenda);
        Assert.Contains("Pedro Souza (sem colete)", legenda);
        // A repetição do João soma na ocorrência dele, sem abrir outra nem alertar de novo.
        var doJoao = await db.OcorrenciasEpiCamera.SingleAsync(o => o.TrabalhadorId == joao.Id);
        Assert.Equal(2, doJoao.VezesVista);
    }

    [Fact]
    public async Task VariasPessoas_TodosJaAlertadosNaHora_GravaSemEnviarNada()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_TodosJaAlertadosNaHora_GravaSemEnviarNada));
        var joao = await ComAlertaAnterior(db, camera, minutosAtras: 15);
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido) { RostosDaCena = () => new[] { RostoEm(0.25, 0.31, joao.Id) } };
        // A segunda pessoa está regular: só o João é irregular nesta cena.
        var analise = new ResultadoAnaliseEpi(2, 1, 0, ConfiancaAnaliseEpi.Alta, new[]
        {
            new PessoaNaCena(0.25, 0.30, true, false),
            new PessoaNaCena(0.75, 0.30, false, false),
        });

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Empty(telegram.Legendas);
        Assert.True(r.Repetida);
        Assert.Equal(2, (await db.OcorrenciasEpiCamera.SingleAsync(o => o.TrabalhadorId == joao.Id)).VezesVista);
    }

    [Fact]
    public async Task VariasPessoas_NinguemReconhecido_UmaOcorrenciaComAsContagensDaCena()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_NinguemReconhecido_UmaOcorrenciaComAsContagensDaCena));
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(DuasPessoasIrregulares), telegram, new FacialFalso(NaoReconhecido))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Null(r.TrabalhadorNome);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal((2, 1, 1), (ocorrencia.PessoasNaCena, ocorrencia.SemCapacete, ocorrencia.SemColete));
        Assert.Null(ocorrencia.TrabalhadorId);
        Assert.EndsWith("Não identificados: 2", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task VariasPessoas_FacialIndisponivel_SegueSemIdentificacao()
    {
        var (db, camera) = await Preparar(nameof(VariasPessoas_FacialIndisponivel_SegueSemIdentificacao));
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(NaoReconhecido) { RostosDaCena = () => throw new HttpRequestException("Azure fora do ar") };

        var r = await Criar(db, new AnalisadorFalso(DuasPessoasIrregulares), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Null((await db.OcorrenciasEpiCamera.SingleAsync()).TrabalhadorId);
    }

    // Semeia uma ocorrência anterior do trabalhador com alerta enviado há `minutosAtras` minutos.
    private static async Task<Trabalhador> ComAlertaAnterior(SstDbContext db, Camera camera, int minutosAtras)
    {
        var trabalhador = new Trabalhador { ObraId = camera.ObraId, Nome = "João da Silva" };
        var quando = DateTime.UtcNow.AddMinutes(-minutosAtras);
        db.AddRange(trabalhador, new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = camera.ObraId, Foto = Jpeg, CapturadaEmUtc = quando, RecebidaEmUtc = quando,
            PessoasNaCena = 1, SemCapacete = 1, TrabalhadorId = trabalhador.Id, IdentificadoPorRosto = true,
            AlertaEnviado = true, AlertaEnviadoEmUtc = quando,
        });
        await db.SaveChangesAsync();
        return trabalhador;
    }

    [Fact]
    public async Task MesmoTrabalhadorAlertadoHaMenosDeUmaHora_GravaOcorrenciaSemNovoAlerta()
    {
        var (db, camera) = await Preparar(nameof(MesmoTrabalhadorAlertadoHaMenosDeUmaHora_GravaOcorrenciaSemNovoAlerta));
        var trabalhador = await ComAlertaAnterior(db, camera, minutosAtras: 20);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Equal("João da Silva", r.TrabalhadorNome);
        Assert.Empty(telegram.Legendas);
        // Mesmo trabalhador, mesma câmera, menos de uma hora: soma na ocorrência já aberta.
        Assert.True(r.Repetida);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(r.OcorrenciaId, ocorrencia.Id);
        Assert.Equal(2, ocorrencia.VezesVista);
        Assert.NotNull(ocorrencia.UltimaVezEmUtc);
    }

    // Semeia uma ocorrência sem trabalhador, vista pela última vez há `minutosAtras` minutos.
    private static async Task<OcorrenciaEpiCamera> ComOcorrenciaSemNome(SstDbContext db, Camera camera, int minutosAtras, bool alertaEnviado)
    {
        var quando = DateTime.UtcNow.AddMinutes(-minutosAtras);
        var ocorrencia = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = camera.ObraId, Foto = new byte[] { 0xFF, 0xD8, 0x77 },
            CapturadaEmUtc = quando, RecebidaEmUtc = quando, UltimaVezEmUtc = quando,
            PessoasNaCena = 1, SemCapacete = 1, AlertaEnviado = alertaEnviado, AlertaEnviadoEmUtc = alertaEnviado ? quando : null,
        };
        db.Add(ocorrencia);
        await db.SaveChangesAsync();
        return ocorrencia;
    }

    [Fact]
    public async Task SemIdentificacao_VistaDeNovoEmPoucosMinutos_SomaNaMesmaOcorrenciaSemNovoAlerta()
    {
        var (db, camera) = await Preparar(nameof(SemIdentificacao_VistaDeNovoEmPoucosMinutos_SomaNaMesmaOcorrenciaSemNovoAlerta),
            c => c.IntervaloMinimoAlertasMinutos = 0);
        var aberta = await ComOcorrenciaSemNome(db, camera, minutosAtras: 2, alertaEnviado: true);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(() => new ResultadoAnaliseEpi(1, 1, 1, ConfiancaAnaliseEpi.Alta)), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.Repetida);
        Assert.False(r.AlertaEnviado);
        Assert.Empty(telegram.Legendas);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(aberta.Id, ocorrencia.Id);
        Assert.Equal(2, ocorrencia.VezesVista);
        // Fica o pior caso das contagens; a foto continua a da primeira vez.
        Assert.Equal((1, 1), (ocorrencia.SemCapacete, ocorrencia.SemColete));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0x77 }, ocorrencia.Foto);
    }

    [Fact]
    public async Task SemIdentificacao_DepoisDeUmaPausa_AbreNovaOcorrencia()
    {
        var (db, camera) = await Preparar(nameof(SemIdentificacao_DepoisDeUmaPausa_AbreNovaOcorrencia),
            c => c.IntervaloMinimoAlertasMinutos = 0);
        await ComOcorrenciaSemNome(db, camera, minutosAtras: 8, alertaEnviado: true);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(() => new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta)), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.False(r.Repetida);
        Assert.True(r.AlertaEnviado);
        Assert.Equal(2, await db.OcorrenciasEpiCamera.CountAsync());
    }

    [Fact]
    public async Task SemIdentificacao_RepeticaoAlertaQuandoOPrimeiroAlertaNaoSaiu()
    {
        var (db, camera) = await Preparar(nameof(SemIdentificacao_RepeticaoAlertaQuandoOPrimeiroAlertaNaoSaiu),
            c => c.IntervaloMinimoAlertasMinutos = 0);
        await ComOcorrenciaSemNome(db, camera, minutosAtras: 1, alertaEnviado: false);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(() => new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta)), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.Repetida);
        Assert.True(r.AlertaEnviado);
        Assert.True((await db.OcorrenciasEpiCamera.SingleAsync()).AlertaEnviado);
    }

    [Fact]
    public async Task OutraCamera_NaoSomaNaOcorrenciaDaPrimeira()
    {
        var (db, camera) = await Preparar(nameof(OutraCamera_NaoSomaNaOcorrenciaDaPrimeira));
        var outra = new Camera { ObraId = camera.ObraId, Nome = "Portaria" };
        db.Add(outra);
        await db.SaveChangesAsync();
        await ComOcorrenciaSemNome(db, outra, minutosAtras: 1, alertaEnviado: true);

        var r = await Criar(db, new AnalisadorFalso(() => new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta)), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.False(r.Repetida);
        Assert.Equal(2, await db.OcorrenciasEpiCamera.CountAsync());
    }

    [Fact]
    public async Task ConsumoDaIa_SomaTokensNoTotalENoDia_InclusiveEmCenaRegular()
    {
        var (db, camera) = await Preparar(nameof(ConsumoDaIa_SomaTokensNoTotalENoDia_InclusiveEmCenaRegular), c =>
        {
            // Consumo de um dia anterior: o do dia zera, o total continua.
            c.TotalTokensEntrada = 1000;
            c.TotalTokensSaida = 100;
            c.DiaDoConsumo = DateTime.UtcNow.AddDays(-2).Date;
            c.AnalisesNoDia = 9;
            c.TokensEntradaNoDia = 900;
            c.TokensSaidaNoDia = 90;
        });
        var analise = new ResultadoAnaliseEpi(2, 0, 0, ConfiancaAnaliseEpi.Alta, Uso: new UsoIa(1800, 60));

        await Criar(db, new AnalisadorFalso(() => analise), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        var lida = await db.Cameras.SingleAsync();
        Assert.Equal((2800, 160), (lida.TotalTokensEntrada, lida.TotalTokensSaida));
        Assert.Equal((1, 1800, 60), (lida.AnalisesNoDia, lida.TokensEntradaNoDia, lida.TokensSaidaNoDia));
        Assert.Equal(DateTime.UtcNow.AddHours(-3).Date, lida.DiaDoConsumo);
        // 2.800 de entrada a US$ 0,10 e 160 de saída a US$ 0,50 por milhão.
        Assert.Equal(0.0004m, CustoIaCameras.EmDolares(lida.TotalTokensEntrada, lida.TotalTokensSaida));
    }

    [Fact]
    public async Task MesmoTrabalhadorAlertadoHaMaisDeUmaHora_AlertaDeNovo()
    {
        var (db, camera) = await Preparar(nameof(MesmoTrabalhadorAlertadoHaMaisDeUmaHora_AlertaDeNovo));
        var trabalhador = await ComAlertaAnterior(db, camera, minutosAtras: 61);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        Assert.Single(telegram.Legendas);
    }

    [Fact]
    public async Task OutroTrabalhadorIdentificado_AlertaMesmoDentroDoIntervaloDaCamera()
    {
        var (db, camera) = await Preparar(nameof(OutroTrabalhadorIdentificado_AlertaMesmoDentroDoIntervaloDaCamera), c =>
        {
            c.IntervaloMinimoAlertasMinutos = 30;
            c.UltimoAlertaEmUtc = DateTime.UtcNow.AddMinutes(-2);
        });
        await ComAlertaAnterior(db, camera, minutosAtras: 2);
        var outro = new Trabalhador { ObraId = camera.ObraId, Nome = "Pedro Souza" };
        db.Add(outro);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, new FacialFalso(() => Reconhecido(outro.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        Assert.EndsWith("Trabalhador: Pedro Souza (identificado pelo rosto)", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task VinculoManualDepoisDoRosto_PassaASerManual()
    {
        var (db, camera) = await Preparar(nameof(VinculoManualDepoisDoRosto_PassaASerManual));
        var apontado = new Trabalhador { ObraId = camera.ObraId, Nome = "João da Silva" };
        var correto = new Trabalhador { ObraId = camera.ObraId, Nome = "Pedro Souza" };
        db.AddRange(apontado, correto);
        await db.SaveChangesAsync();
        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), new TelegramFalso(), new FacialFalso(() => Reconhecido(apontado.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        await new VincularTrabalhadorOcorrenciaEpiCommandHandler(db).Handle(
            new VincularTrabalhadorOcorrenciaEpiCommand(r.OcorrenciaId!.Value, correto.Id, null), default);

        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(correto.Id, ocorrencia.TrabalhadorId);
        Assert.False(ocorrencia.IdentificadoPorRosto);
        Assert.Null(ocorrencia.ConfiancaIdentificacao);
        Assert.Equal(SituacaoOcorrenciaEpi.Conferida, ocorrencia.Situacao);
    }

    private static ResultadoAnaliseEpi SemCapacete() => new(2, 1, 0, ConfiancaAnaliseEpi.Alta);
    private static ResultadoAnaliseEpi CenaRegular() => new(2, 0, 0, ConfiancaAnaliseEpi.Alta);

    [Fact]
    public async Task ComIrregularidade_GravaOcorrenciaEEnviaAlerta()
    {
        var (db, camera) = await Preparar(nameof(ComIrregularidade_GravaOcorrenciaEEnviaAlerta));
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(r.OcorrenciaId, ocorrencia.Id);
        Assert.Equal(camera.ObraId, ocorrencia.ObraId);
        Assert.Equal(Jpeg, ocorrencia.Foto);
        Assert.Equal(1, ocorrencia.SemCapacete);
        Assert.True(ocorrencia.AlertaEnviado);
        Assert.NotNull(ocorrencia.AlertaEnviadoEmUtc);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("Obra Beira Rio · Câmera Betoneira", legenda);
        Assert.Contains("sem capacete", legenda);
    }

    [Fact]
    public async Task CenaRegular_NaoGravaFotoNemEnviaAlerta()
    {
        var (db, camera) = await Preparar(nameof(CenaRegular_NaoGravaFotoNemEnviaAlerta));
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(CenaRegular), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
        var lida = await db.Cameras.SingleAsync();
        Assert.NotNull(lida.UltimaAnaliseEmUtc);
        Assert.Equal(1, lida.TotalAnalises);
    }

    [Fact]
    public async Task AnaliseRecente_IgnoraSemChamarAIa()
    {
        var (db, camera) = await Preparar(nameof(AnaliseRecente_IgnoraSemChamarAIa),
            c => c.UltimaAnaliseEmUtc = DateTime.UtcNow.AddMilliseconds(-200));
        var analisador = new AnalisadorFalso(SemCapacete);

        var r = await Criar(db, analisador, new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ignorado, r.Situacao);
        Assert.Equal(0, analisador.Chamadas);
        Assert.Equal(0, (await db.Cameras.SingleAsync()).TotalAnalises);
    }

    [Fact]
    public async Task FalhaDaIa_DevolveFalhaSemOcorrenciaNemAlerta()
    {
        var (db, camera) = await Preparar(nameof(FalhaDaIa_DevolveFalhaSemOcorrenciaNemAlerta));
        var telegram = new TelegramFalso();
        var analisador = new AnalisadorFalso(() => throw new InvalidOperationException("IA fora do ar"));

        var r = await Criar(db, analisador, telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Falha, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
        Assert.Equal(1, (await db.Cameras.SingleAsync()).TotalAnalises);
    }

    [Fact]
    public async Task TelegramRecusa_OcorrenciaFicaGravadaComAlertaNaoEnviado()
    {
        var (db, camera) = await Preparar(nameof(TelegramRecusa_OcorrenciaFicaGravadaComAlertaNaoEnviado));
        var telegram = new TelegramFalso { Resultado = false };

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.False(ocorrencia.AlertaEnviado);
        Assert.Null((await db.Cameras.SingleAsync()).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task DentroDoIntervaloEntreAlertas_GravaOcorrenciaSemEnviar()
    {
        var (db, camera) = await Preparar(nameof(DentroDoIntervaloEntreAlertas_GravaOcorrenciaSemEnviar), c =>
        {
            c.IntervaloMinimoAlertasMinutos = 10;
            c.UltimoAlertaEmUtc = DateTime.UtcNow.AddMinutes(-2);
        });
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Single(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
    }

    [Fact]
    public async Task SemLimiteDeIntervalo_EnviaMesmoComAlertaRecente()
    {
        var (db, camera) = await Preparar(nameof(SemLimiteDeIntervalo_EnviaMesmoComAlertaRecente), c =>
        {
            c.IntervaloMinimoAlertasMinutos = 0;
            c.UltimoAlertaEmUtc = DateTime.UtcNow.AddSeconds(-40);
        });
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        Assert.Single(telegram.Legendas);
    }

    [Fact]
    public async Task CameraDesabilitada_LancaInvalidOperationException()
    {
        var (db, camera) = await Preparar(nameof(CameraDesabilitada_LancaInvalidOperationException), c => c.Habilitada = false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(db, new AnalisadorFalso(SemCapacete), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CameraInexistente_LancaKeyNotFoundException()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(CameraInexistente_LancaKeyNotFoundException));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Criar(db, new AnalisadorFalso(SemCapacete), new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(Guid.NewGuid(), Jpeg, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task TelegramLancaExcecao_OcorrenciaFicaGravadaComAlertaNaoEnviado()
    {
        var (db, camera) = await Preparar(nameof(TelegramLancaExcecao_OcorrenciaFicaGravadaComAlertaNaoEnviado));
        var telegram = new TelegramQueLanca();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.False((await db.OcorrenciasEpiCamera.SingleAsync()).AlertaEnviado);
    }

    [Fact]
    public async Task ConflitoAoAtualizarCameraDepoisDoAlerta_OcorrenciaContinuaGravadaComAlertaEnviado()
    {
        var interceptor = new ConflitoNaCameraInterceptor(nameof(Camera.UltimoAlertaEmUtc));
        var (db, camera) = await Preparar(nameof(ConflitoAoAtualizarCameraDepoisDoAlerta_OcorrenciaContinuaGravadaComAlertaEnviado),
            interceptor: interceptor);
        interceptor.Armado = true;
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(SemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Single(telegram.Legendas);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.True(ocorrencia.AlertaEnviado);
        Assert.NotNull(ocorrencia.AlertaEnviadoEmUtc);
    }

    [Fact]
    public async Task ConflitoNoPrimeiroSave_DevolveIgnoradoSemChamarAIa()
    {
        var interceptor = new ConflitoNaCameraInterceptor(nameof(Camera.UltimaAnaliseEmUtc));
        var (db, camera) = await Preparar(nameof(ConflitoNoPrimeiroSave_DevolveIgnoradoSemChamarAIa), interceptor: interceptor);
        interceptor.Armado = true;
        var analisador = new AnalisadorFalso(SemCapacete);

        var r = await Criar(db, analisador, new TelegramFalso())
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ignorado, r.Situacao);
        Assert.Equal(0, analisador.Chamadas);
    }

    private sealed class TelegramQueLanca : ITelegramResumoService
    {
        public Task EnviarAsync(string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default)
            => throw new HttpRequestException("Telegram fora do ar");
    }

    [Fact]
    public void Validador_ExigeJpegDentroDoLimite()
    {
        var validador = new AnalisarQuadroCameraCommandValidator();
        var id = Guid.NewGuid();

        Assert.True(validador.Validate(new AnalisarQuadroCameraCommand(id, Jpeg, DateTime.UtcNow)).IsValid);
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, Array.Empty<byte>(), DateTime.UtcNow)).IsValid);
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, new byte[] { 0x89, 0x50, 0x4E, 0x47 }, DateTime.UtcNow)).IsValid);
        var grande = new byte[4_000_001];
        grande[0] = 0xFF;
        grande[1] = 0xD8;
        Assert.False(validador.Validate(new AnalisarQuadroCameraCommand(id, grande, DateTime.UtcNow)).IsValid);
    }

    [Fact]
    public async Task TimeoutDaIa_DevolveFalhaSemOcorrenciaNemAlerta()
    {
        var (db, camera) = await Preparar(nameof(TimeoutDaIa_DevolveFalhaSemOcorrenciaNemAlerta));
        var telegram = new TelegramFalso();
        var analisador = new AnalisadorFalso(() => throw new TaskCanceledException("timeout"));

        var r = await Criar(db, analisador, telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), CancellationToken.None);

        Assert.Equal(SituacaoQuadro.Falha, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
    }

    [Fact]
    public async Task AvisarTelegramDesligado_GravaOcorrenciaMasNaoEnviaNemMarcaAlertaEnviado()
    {
        var (db, camera) = await Preparar(nameof(AvisarTelegramDesligado_GravaOcorrenciaMasNaoEnviaNemMarcaAlertaEnviado),
            c => c.AvisarTelegram = false);
        var telegram = new TelegramFalso();
        var analisador = new AnalisadorFalso(UmaPessoaSemCapacete);

        var r = await Criar(db, analisador, telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(1, analisador.Chamadas);
        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.NotNull(r.OcorrenciaId);
        Assert.False(r.AlertaEnviado);
        Assert.Empty(telegram.Legendas);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(Jpeg, ocorrencia.Foto);
        Assert.False(ocorrencia.AlertaEnviado);
        Assert.Null(ocorrencia.AlertaEnviadoEmUtc);
        Assert.Null((await db.Cameras.SingleAsync()).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task AvisarTelegramDesligado_CenaComVariasPessoas_TambemNaoEnvia()
    {
        var (db, camera) = await Preparar(nameof(AvisarTelegramDesligado_CenaComVariasPessoas_TambemNaoEnvia), c => c.AvisarTelegram = false);
        var telegram = new TelegramFalso();
        var analisador = new AnalisadorFalso(() => new ResultadoAnaliseEpi(2, 1, 0, ConfiancaAnaliseEpi.Alta,
            Detalhe: new[] { new PessoaNaCena(0.2, 0.2, SemCapacete: true, SemColete: false), new PessoaNaCena(0.6, 0.2, false, false) }));

        var r = await Criar(db, analisador, telegram).Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Empty(telegram.Legendas);
        Assert.False((await db.OcorrenciasEpiCamera.SingleAsync()).AlertaEnviado);
    }

    [Fact]
    public async Task AvisarTelegramLigado_EnviaEMarcaAlertaEnviadoComoSempre()
    {
        var (db, camera) = await Preparar(nameof(AvisarTelegramLigado_EnviaEMarcaAlertaEnviadoComoSempre));
        Assert.True(camera.AvisarTelegram);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        Assert.Single(telegram.Legendas);
        Assert.True((await db.OcorrenciasEpiCamera.SingleAsync()).AlertaEnviado);
    }

    // Pessoa de que só aparecem as pernas: nada foi julgado.
    private static ResultadoAnaliseEpi SoPernas() => new(1, 0, 0, ConfiancaAnaliseEpi.Alta,
        new[] { new PessoaNaCena(0.5, 0.95, false, false, CabecaVisivel: false, TroncoVisivel: false, CapaceteNaoAvaliavel: true, ColeteNaoAvaliavel: true) },
        CapaceteNaoAvaliavel: 1, ColeteNaoAvaliavel: 1);

    [Fact]
    public async Task SoPernasNaFoto_CenaLimpaSemOcorrenciaEMensagemNoTelegram()
    {
        var (db, camera) = await Preparar(nameof(SoPernasNaFoto_CenaLimpaSemOcorrenciaEMensagemNoTelegram));
        var telegram = new TelegramFalso();
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        var r = await Criar(db, new AnalisadorFalso(SoPernas), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.False(r.AlertaEnviado);
        Assert.Equal(1, r.Pessoas);
        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(0, r.SemColete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        Assert.Equal(1, r.ColeteNaoAvaliavel);
        Assert.Empty(telegram.Legendas);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Equal(0, facial.Chamadas);
    }

    [Fact]
    public async Task TroncoSemColeteECabecaForaDaImagem_AlertaDeColeteComALinhaDeCapaceteNaoConferido()
    {
        var (db, camera) = await Preparar(nameof(TroncoSemColeteECabecaForaDaImagem_AlertaDeColeteComALinhaDeCapaceteNaoConferido));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(1, 0, 1, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.0, false, true, CabecaVisivel: false, TroncoVisivel: true, CapaceteNaoAvaliavel: true) },
            CapaceteNaoAvaliavel: 1);

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Equal(0, r.SemCapacete);
        Assert.Equal(1, r.SemColete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(0, ocorrencia.SemCapacete);
        Assert.Equal(1, ocorrencia.SemColete);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.StartsWith("⚠️ EPI — pessoa sem colete\n", legenda);
        Assert.Contains("Capacete não conferido", legenda);
        Assert.DoesNotContain("Colete não conferido", legenda);
        Assert.DoesNotContain("sem capacete", legenda);
    }

    [Fact]
    public async Task PessoaInteiraSemCapacete_AlertaNormalSemLinhaDeNaoConferido()
    {
        var (db, camera) = await Preparar(nameof(PessoaInteiraSemCapacete_AlertaNormalSemLinhaDeNaoConferido));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.2, true, false, Caixa: new CaixaPessoa(0.3, 0.1, 0.7, 0.9)) });

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(0, r.CapaceteNaoAvaliavel);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.StartsWith("⚠️ EPI — pessoa sem capacete\n", legenda);
        Assert.DoesNotContain("não conferido", legenda);
    }

    [Fact]
    public async Task CenaComVariasPessoas_ItemNaoAvaliavelNaoViraIrregularidadeELegendaTrazALinha()
    {
        var (db, camera) = await Preparar(nameof(CenaComVariasPessoas_ItemNaoAvaliavelNaoViraIrregularidadeELegendaTrazALinha));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(2, 1, 0, ConfiancaAnaliseEpi.Alta, new[]
        {
            new PessoaNaCena(0.25, 0.30, true, false),
            new PessoaNaCena(0.75, 0.0, false, false, CabecaVisivel: false, CapaceteNaoAvaliavel: true),
        }, CapaceteNaoAvaliavel: 1);

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.Equal(1, r.SemCapacete);
        Assert.Equal(1, r.CapaceteNaoAvaliavel);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("Capacete não conferido", legenda);
    }

    // ---- Óculos e cor do capacete por função (14/11/2026) ----

    private static PessoaNaCena PessoaEmOrdem(CorCapacete? cor, double x = 0.5)
        => new(x, 0.30, false, false, CorCapaceteVista: cor);

    private static ResultadoAnaliseEpi UmaPessoaComCor(CorCapacete? cor)
        => new(1, 0, 0, ConfiancaAnaliseEpi.Alta, new[] { PessoaEmOrdem(cor) });

    // Trabalhador com função que tem (ou não) cor de capacete.
    private static async Task<Trabalhador> ComFuncaoDeCor(SstDbContext db, Camera camera, CorCapacete corDaFuncao, string funcao = "Pedreiro", string nome = "João da Silva")
    {
        var f = new Funcao { Nome = funcao, CorCapacete = corDaFuncao };
        var t = new Trabalhador { ObraId = camera.ObraId, Nome = nome, FuncaoId = f.Id };
        db.AddRange(f, t);
        await db.SaveChangesAsync();
        return t;
    }

    [Fact]
    public async Task Oculos_NaoConforme_GeraOcorrenciaEAlertaNoTelegram()
    {
        var (db, camera) = await Preparar(nameof(Oculos_NaoConforme_GeraOcorrenciaEAlertaNoTelegram));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(1, 0, 0, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.3, false, false, SemOculos: true) }, SemOculos: 1);

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AlertaEnviado);
        Assert.Equal(1, r.SemOculos);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(1, ocorrencia.SemOculos);
        Assert.Equal((0, 0), (ocorrencia.SemCapacete, ocorrencia.SemColete));
        Assert.False(ocorrencia.AvisoCorCapacete);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.StartsWith("⚠️ EPI — pessoa sem óculos\n", legenda);
        Assert.Contains("1 pessoa na cena, 1 sem óculos", legenda);
    }

    [Fact]
    public async Task Oculos_NaoAvaliavel_NaoGeraOcorrenciaNemAlerta()
    {
        var (db, camera) = await Preparar(nameof(Oculos_NaoAvaliavel_NaoGeraOcorrenciaNemAlerta));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(1, 0, 0, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.3, false, false, OculosNaoAvaliavel: true) }, OculosNaoAvaliavel: 1);

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Equal(1, r.OculosNaoAvaliavel);
        Assert.Empty(telegram.Legendas);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task Oculos_NaoAvaliavelNaLegendaDeAlertaMotivadoPorOutroItem()
    {
        var (db, camera) = await Preparar(nameof(Oculos_NaoAvaliavelNaLegendaDeAlertaMotivadoPorOutroItem));
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.3, true, false, OculosNaoAvaliavel: true) }, OculosNaoAvaliavel: 1);

        await Criar(db, new AnalisadorFalso(() => analise), telegram)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Contains("Óculos não conferido em 1 pessoa", Assert.Single(telegram.Legendas));
    }

    [Fact]
    public async Task Cor_PessoaIdentificadaComFuncaoDeOutraCor_GravaAvisoSemTelegramEComGravidadeMenor()
    {
        var (db, camera) = await Preparar(nameof(Cor_PessoaIdentificadaComFuncaoDeOutraCor_GravaAvisoSemTelegramEComGravidadeMenor));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AvisoCorCapacete);
        Assert.False(r.AlertaEnviado);
        Assert.Equal(trabalhador.Id, r.TrabalhadorId);
        Assert.Empty(telegram.Legendas);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.True(ocorrencia.AvisoCorCapacete);
        Assert.Equal(CorCapacete.Azul, ocorrencia.CorCapaceteVista);
        Assert.Equal(CorCapacete.Amarelo, ocorrencia.CorCapaceteEsperada);
        Assert.Equal((0, 0, 0), (ocorrencia.SemCapacete, ocorrencia.SemColete, ocorrencia.SemOculos));
        Assert.False(ocorrencia.AlertaEnviado);
        Assert.Null((await db.Cameras.SingleAsync()).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task Cor_IgualAEsperada_NaoGeraAviso()
    {
        var (db, camera) = await Preparar(nameof(Cor_IgualAEsperada_NaoGeraAviso));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var telegram = new TelegramFalso();

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Amarelo)), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
        Assert.Empty(telegram.Legendas);
    }

    [Fact]
    public async Task Cor_PessoaNaoIdentificada_NaoAcusaCor()
    {
        var (db, camera) = await Preparar(nameof(Cor_PessoaNaoIdentificada_NaoAcusaCor));
        await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), new FacialFalso(NaoReconhecido))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task Cor_FuncaoSemCorDefinida_NaoGeraAviso()
    {
        var (db, camera) = await Preparar(nameof(Cor_FuncaoSemCorDefinida_NaoGeraAviso));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.SemPadrao, funcao: "Almoxarife");
        // Existe outra função com cor e um trabalhador ativo dela na obra, para a conferência de fato rodar.
        await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo, "Pedreiro", "Outro Trabalhador");

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Empty(await db.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task Cor_NenhumaFuncaoComCor_NaoChamaOReconhecimentoFacial()
    {
        var (db, camera) = await Preparar(nameof(Cor_NenhumaFuncaoComCor_NaoChamaOReconhecimentoFacial));
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Equal(0, facial.Chamadas);
    }

    [Fact]
    public async Task Cor_NaoAvaliavelNaCena_NaoChamaOReconhecimentoFacial()
    {
        var (db, camera) = await Preparar(nameof(Cor_NaoAvaliavelNaCena_NaoChamaOReconhecimentoFacial));
        await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(null)), new TelegramFalso(), facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Equal(0, facial.Chamadas);
    }

    [Fact]
    public async Task Cor_ComIrregularidadeReal_VaiJuntaNaOcorrenciaNormalComTelegram()
    {
        var (db, camera) = await Preparar(nameof(Cor_ComIrregularidadeReal_VaiJuntaNaOcorrenciaNormalComTelegram));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var telegram = new TelegramFalso();
        // Capacete conforme (azul, fora do padrão) e colete faltando: a irregularidade real manda.
        var analise = new ResultadoAnaliseEpi(1, 0, 1, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.3, false, true, CorCapaceteVista: CorCapacete.Azul) });

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.False(r.AvisoCorCapacete);
        Assert.True(r.AlertaEnviado);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.False(ocorrencia.AvisoCorCapacete);
        Assert.Equal(1, ocorrencia.SemColete);
        Assert.Equal(CorCapacete.Azul, ocorrencia.CorCapaceteVista);
        Assert.Equal(CorCapacete.Amarelo, ocorrencia.CorCapaceteEsperada);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.StartsWith("⚠️ EPI — pessoa sem colete\n", legenda);
    }

    [Fact]
    public async Task Cor_IrregularidadeSemIdentificacao_GravaCorVistaSemAcusarEsperada()
    {
        var (db, camera) = await Preparar(nameof(Cor_IrregularidadeSemIdentificacao_GravaCorVistaSemAcusarEsperada));
        await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var analise = new ResultadoAnaliseEpi(1, 0, 1, ConfiancaAnaliseEpi.Alta,
            new[] { new PessoaNaCena(0.5, 0.3, false, true, CorCapaceteVista: CorCapacete.Verde) });

        await Criar(db, new AnalisadorFalso(() => analise), new TelegramFalso(), new FacialFalso(NaoReconhecido))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(CorCapacete.Verde, ocorrencia.CorCapaceteVista);
        Assert.Null(ocorrencia.CorCapaceteEsperada);
        Assert.False(ocorrencia.AvisoCorCapacete);
    }

    [Fact]
    public async Task Cor_AvisoRepetidoDoMesmoTrabalhador_SomaNaOcorrenciaAbertaSemOutraOcorrencia()
    {
        var (db, camera) = await Preparar(nameof(Cor_AvisoRepetidoDoMesmoTrabalhador_SomaNaOcorrenciaAbertaSemOutraOcorrencia));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var agente = Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), new FacialFalso(() => Reconhecido(trabalhador.Id)));

        var primeiro = await agente.Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);
        // O intervalo mínimo entre análises da câmera só barra envio em paralelo, e a identificação só de cor tem limite de
        // 10 minutos por câmera: recua os dois marcadores para o segundo quadro.
        var camRep = await db.Cameras.SingleAsync();
        camRep.UltimaAnaliseEmUtc = DateTime.UtcNow.AddMinutes(-1);
        camRep.UltimaIdentificacaoSoCorEmUtc = DateTime.UtcNow.AddMinutes(-11);
        await db.SaveChangesAsync();
        var segundo = await agente.Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.False(primeiro.Repetida);
        Assert.True(segundo.Repetida);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(2, ocorrencia.VezesVista);
        Assert.True(ocorrencia.AvisoCorCapacete);
    }

    [Fact]
    public async Task Cor_AvisoAbertoMaisIrregularidadeDepois_SaoDuasOcorrenciasEAIrregularTemFotoEHorarioDela()
    {
        var (db, camera) = await Preparar(nameof(Cor_AvisoAbertoMaisIrregularidadeDepois_SaoDuasOcorrenciasEAIrregularTemFotoEHorarioDela));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var aviso = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = camera.ObraId, Foto = new byte[] { 0xFF, 0xD8, 0x55 },
            CapturadaEmUtc = DateTime.UtcNow.AddMinutes(-20), RecebidaEmUtc = DateTime.UtcNow.AddMinutes(-20),
            UltimaVezEmUtc = DateTime.UtcNow.AddMinutes(-20), PessoasNaCena = 1, TrabalhadorId = trabalhador.Id, IdentificadoPorRosto = true,
            AvisoCorCapacete = true, CorCapaceteVista = CorCapacete.Azul, CorCapaceteEsperada = CorCapacete.Amarelo,
        };
        db.Add(aviso);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var foto = new byte[] { 0xFF, 0xD8, 0x66, 0x77 };
        var agora = DateTime.UtcNow;

        var r = await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), telegram, new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, foto, agora), default);

        Assert.False(r.Repetida);
        Assert.True(r.AlertaEnviado);
        var todas = await db.OcorrenciasEpiCamera.ToListAsync();
        Assert.Equal(2, todas.Count);
        var intacto = todas.Single(o => o.Id == aviso.Id);
        Assert.True(intacto.AvisoCorCapacete);
        Assert.Equal(1, intacto.VezesVista);
        Assert.Equal(0, intacto.SemCapacete);
        var irregular = todas.Single(o => o.Id != aviso.Id);
        Assert.False(irregular.AvisoCorCapacete);
        Assert.Equal(foto, irregular.Foto);
        Assert.Equal(agora, irregular.CapturadaEmUtc);
        Assert.Equal(1, irregular.SemCapacete);
        Assert.True(irregular.AlertaEnviado);
        Assert.Single(telegram.Legendas);
    }

    [Fact]
    public async Task Cor_IrregularAbertaMaisAvisoDepois_NaoAlteraVezesVistaNemUltimaVezDaIrregular()
    {
        var (db, camera) = await Preparar(nameof(Cor_IrregularAbertaMaisAvisoDepois_NaoAlteraVezesVistaNemUltimaVezDaIrregular));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var quando = DateTime.UtcNow.AddMinutes(-40);
        var irregular = new OcorrenciaEpiCamera
        {
            CameraId = camera.Id, ObraId = camera.ObraId, Foto = new byte[] { 0xFF, 0xD8, 0x44 },
            CapturadaEmUtc = quando, RecebidaEmUtc = quando, UltimaVezEmUtc = quando, PessoasNaCena = 1, SemColete = 1,
            TrabalhadorId = trabalhador.Id, IdentificadoPorRosto = true, AlertaEnviado = true, AlertaEnviadoEmUtc = quando,
        };
        db.Add(irregular);
        await db.SaveChangesAsync();

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), new FacialFalso(() => Reconhecido(trabalhador.Id)))
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AvisoCorCapacete);
        Assert.False(r.Repetida);
        var todas = await db.OcorrenciasEpiCamera.ToListAsync();
        Assert.Equal(2, todas.Count);
        var intacta = todas.Single(o => o.Id == irregular.Id);
        Assert.Equal(1, intacta.VezesVista);
        Assert.Equal(quando, intacta.UltimaVezEmUtc);
        Assert.False(intacta.AvisoCorCapacete);
        Assert.True(todas.Single(o => o.Id != irregular.Id).AvisoCorCapacete);
    }

    [Fact]
    public async Task Cor_IdentificacaoSoDeCor_NoMaximoUmaACada10MinutosPorCamera()
    {
        var (db, camera) = await Preparar(nameof(Cor_IdentificacaoSoDeCor_NoMaximoUmaACada10MinutosPorCamera));
        var trabalhador = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var facial = new FacialFalso(() => Reconhecido(trabalhador.Id));
        var handler = Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Amarelo)), new TelegramFalso(), facial);

        await handler.Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);
        Assert.Equal(1, facial.Chamadas);
        Assert.NotNull((await db.Cameras.SingleAsync()).UltimaIdentificacaoSoCorEmUtc);

        // Segundo quadro 5 minutos depois: o limite barra a identificação e a cena sai limpa.
        var cam = await db.Cameras.SingleAsync();
        cam.UltimaAnaliseEmUtc = DateTime.UtcNow.AddMinutes(-5);
        cam.UltimaIdentificacaoSoCorEmUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        var r2 = await handler.Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);
        Assert.Equal(SituacaoQuadro.Limpo, r2.Situacao);
        Assert.Equal(1, facial.Chamadas);

        // Passados 10 minutos, identifica de novo.
        cam.UltimaAnaliseEmUtc = DateTime.UtcNow.AddMinutes(-1);
        cam.UltimaIdentificacaoSoCorEmUtc = DateTime.UtcNow.AddMinutes(-11);
        await db.SaveChangesAsync();
        await handler.Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);
        Assert.Equal(2, facial.Chamadas);
    }

    [Fact]
    public async Task Cor_CenaComIrregularidade_IdentificaMesmoDentroDosDezMinutos()
    {
        var (db, camera) = await Preparar(nameof(Cor_CenaComIrregularidade_IdentificaMesmoDentroDosDezMinutos),
            c => c.UltimaIdentificacaoSoCorEmUtc = DateTime.UtcNow.AddMinutes(-1));
        await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo);
        var facial = new FacialFalso(NaoReconhecido);

        await Criar(db, new AnalisadorFalso(UmaPessoaSemCapacete), new TelegramFalso(), facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(1, facial.Chamadas);
    }

    [Fact]
    public async Task Cor_SoFuncaoComCorDeTrabalhadorDeOutraObraOuDemitido_NaoChamaOReconhecimentoFacial()
    {
        var (db, camera) = await Preparar(nameof(Cor_SoFuncaoComCorDeTrabalhadorDeOutraObraOuDemitido_NaoChamaOReconhecimentoFacial));
        var funcao = new Funcao { Nome = "Pedreiro", CorCapacete = CorCapacete.Amarelo };
        db.AddRange(funcao,
            new Trabalhador { ObraId = Guid.NewGuid(), Nome = "De outra obra", FuncaoId = funcao.Id },
            new Trabalhador { ObraId = camera.ObraId, Nome = "Demitido", FuncaoId = funcao.Id, DataDemissao = DateTime.UtcNow.AddDays(-3) });
        await db.SaveChangesAsync();
        var facial = new FacialFalso(() => Reconhecido(Guid.NewGuid()));

        var r = await Criar(db, new AnalisadorFalso(() => UmaPessoaComCor(CorCapacete.Azul)), new TelegramFalso(), facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Limpo, r.Situacao);
        Assert.Equal(0, facial.Chamadas);
        Assert.Null((await db.Cameras.SingleAsync()).UltimaIdentificacaoSoCorEmUtc);
    }

    [Fact]
    public async Task Cor_VariasPessoas_AvisaSoQuemFoiIdentificadoComCorErradaESemTelegram()
    {
        var (db, camera) = await Preparar(nameof(Cor_VariasPessoas_AvisaSoQuemFoiIdentificadoComCorErradaESemTelegram));
        var joao = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo, "Pedreiro", "João da Silva");
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(2, 0, 0, ConfiancaAnaliseEpi.Alta, new[]
        {
            PessoaEmOrdem(CorCapacete.Azul, x: 0.25),    // identificado, cor errada: aviso
            PessoaEmOrdem(CorCapacete.Azul, x: 0.75),    // não identificado: não se acusa
        });
        var facial = new FacialFalso(NaoReconhecido)
        {
            RostosDaCena = () => new[] { RostoEm(0.25, 0.31, joao.Id), RostoEm(0.75, 0.31, null) },
        };

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r.Situacao);
        Assert.True(r.AvisoCorCapacete);
        Assert.False(r.AlertaEnviado);
        Assert.Empty(telegram.Legendas);
        var ocorrencia = await db.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(joao.Id, ocorrencia.TrabalhadorId);
        Assert.True(ocorrencia.AvisoCorCapacete);
    }

    [Fact]
    public async Task Cor_VariasPessoasComIrregularidadeReal_AvisoDeCorNaoEntraNoTelegram()
    {
        var (db, camera) = await Preparar(nameof(Cor_VariasPessoasComIrregularidadeReal_AvisoDeCorNaoEntraNoTelegram));
        var joao = await ComFuncaoDeCor(db, camera, CorCapacete.Amarelo, "Pedreiro", "João da Silva");
        var pedro = await ComFuncaoDeCor(db, camera, CorCapacete.Branco, "Engenheiro", "Pedro Souza");
        var telegram = new TelegramFalso();
        var analise = new ResultadoAnaliseEpi(2, 1, 0, ConfiancaAnaliseEpi.Alta, new[]
        {
            new PessoaNaCena(0.25, 0.30, SemCapacete: true, SemColete: false),   // João sem capacete: alerta real
            PessoaEmOrdem(CorCapacete.Amarelo, x: 0.75),                         // Pedro (branco) de amarelo: só aviso
        });
        var facial = new FacialFalso(NaoReconhecido)
        {
            RostosDaCena = () => new[] { RostoEm(0.25, 0.31, joao.Id), RostoEm(0.75, 0.31, pedro.Id) },
        };

        var r = await Criar(db, new AnalisadorFalso(() => analise), telegram, facial)
            .Handle(new AnalisarQuadroCameraCommand(camera.Id, Jpeg, DateTime.UtcNow), default);

        Assert.True(r.AlertaEnviado);
        var legenda = Assert.Single(telegram.Legendas);
        Assert.Contains("João da Silva (sem capacete)", legenda);
        Assert.DoesNotContain("Pedro Souza", legenda);
        var doPedro = await db.OcorrenciasEpiCamera.SingleAsync(o => o.TrabalhadorId == pedro.Id);
        Assert.True(doPedro.AvisoCorCapacete);
        Assert.False(doPedro.AlertaEnviado);
        Assert.False((await db.OcorrenciasEpiCamera.SingleAsync(o => o.TrabalhadorId == joao.Id)).AvisoCorCapacete);
    }
}
