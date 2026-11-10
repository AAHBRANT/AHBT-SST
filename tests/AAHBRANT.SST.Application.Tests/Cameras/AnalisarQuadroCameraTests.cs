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
            c => c.UltimaAnaliseEmUtc = DateTime.UtcNow.AddSeconds(-5));
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
}
