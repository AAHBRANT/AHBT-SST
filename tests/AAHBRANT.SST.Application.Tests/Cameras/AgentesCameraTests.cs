using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Agente de câmeras instalado na obra: cadastro/revogação (gestão), autenticação pelo segredo e envio de
// quadros SEM usuário logado. O ponto crítico é o escopo: com o Entra ligado, rota anônima chega com o escopo
// zerado e os filtros globais escondem tudo; o handler precisa definir o escopo da obra do agente.
public class AgentesCameraTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20 };
    private static readonly ISegredoDispositivoHasher Hasher = new SegredoDispositivoHasherService();

    private sealed class AnalisadorFalso : IAnalisadorEpiImagem
    {
        public Func<ResultadoAnaliseEpi> Resposta { get; set; } = () => new ResultadoAnaliseEpi(1, 1, 0, ConfiancaAnaliseEpi.Alta);
        public int Chamadas { get; private set; }
        public Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(Resposta());
        }
    }

    private sealed class TelegramFalso : ITelegramResumoService
    {
        public int Fotos { get; private set; }
        public List<string> Legendas { get; } = new();
        public Task EnviarAsync(string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default)
        {
            Fotos++;
            Legendas.Add(legenda);
            return Task.FromResult(true);
        }
    }

    // Reconhece sempre o trabalhador informado (como a galeria facial da obra faria).
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        private readonly Guid? _trabalhadorId;
        public Guid? ObraConsultada { get; private set; }
        public FacialFalso(Guid? trabalhadorId) => _trabalhadorId = trabalhadorId;

        public Task<ResultadoIdentificacaoFacial> IdentificarSemRegistrarFalhaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        {
            ObraConsultada = obraId;
            return Task.FromResult(_trabalhadorId is { } id
                ? new ResultadoIdentificacaoFacial(true, new ResultadoAutenticacaoAssinatura(id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial, Confianca: 93), null, 0.93)
                : new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null));
        }

        public Task<IReadOnlyList<RostoNaCena>> IdentificarRostosDaCenaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RostoNaCena>>(Array.Empty<RostoNaCena>());
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true) => throw new NotSupportedException();
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => throw new NotSupportedException();
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
    }

    // Repassa o comando interno (AnalisarQuadroCameraCommand) ao handler real, sem montar o MediatR inteiro.
    private sealed class SenderParaHandler : ISender
    {
        private readonly AnalisarQuadroCameraCommandHandler _handler;
        public SenderParaHandler(AnalisarQuadroCameraCommandHandler handler) => _handler = handler;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var comando = Assert.IsType<AnalisarQuadroCameraCommand>(request);
            return (TResponse)(object)await _handler.Handle(comando, cancellationToken);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static DbContextOptions<SstDbContext> Opcoes(string nomeBanco, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    // Simula o conflito de versão da linha do agente (o InMemory não aplica rowversion).
    private sealed class ConflitoNoAgenteInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AgenteCamera>().Any(e => e.State == EntityState.Modified))
                throw new DbUpdateConcurrencyException("Conflito simulado no agente.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record Cenario(
        SstDbContext Anonimo, CurrentUserService Escopo, Obra ObraA, Obra ObraB, Camera CameraA, Camera CameraB,
        AgenteCamera AgenteA, string SegredoA, AnalisadorFalso Analisador, TelegramFalso Telegram,
        AnalisarQuadroAgenteCameraCommandHandler Handler, SstDbContext Global);

    // Duas obras no mesmo banco. O contexto "Anonimo" usa o escopo ZERADO (false, sem obras): é exatamente o que o
    // EscopoPorObraMiddleware deixa numa rota anônima com AzureAd:TenantId preenchido (produção/hml).
    private static async Task<Cenario> Preparar(string nomeBanco, Guid? trabalhadorReconhecido = null,
        Func<Obra, Guid?>? trabalhadorDaObraA = null, IInterceptor? interceptor = null)
    {
        var opcoes = Opcoes(nomeBanco, interceptor);
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var cameraA = new Camera { ObraId = obraA.Id, Nome = "Betoneira" };
        var cameraB = new Camera { ObraId = obraB.Id, Nome = "Portaria B" };
        var segredo = Hasher.GerarSegredo();
        var agenteA = new AgenteCamera { ObraId = obraA.Id, Nome = "PC da obra A", SegredoHash = Hasher.GerarHash(segredo) };
        global.AddRange(obraA, obraB, cameraA, cameraB, agenteA);
        await global.SaveChangesAsync();

        var escopo = new CurrentUserService();
        escopo.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
        var anonimo = new SstDbContext(opcoes, escopo);

        var analisador = new AnalisadorFalso();
        var telegram = new TelegramFalso();
        var interno = new AnalisarQuadroCameraCommandHandler(anonimo, analisador, telegram,
            new FacialFalso(trabalhadorReconhecido), NullLogger<AnalisarQuadroCameraCommandHandler>.Instance);
        var handler = new AnalisarQuadroAgenteCameraCommandHandler(anonimo, new AgenteCameraAutenticador(anonimo, Hasher),
            escopo, new SenderParaHandler(interno), NullLogger<AnalisarQuadroAgenteCameraCommandHandler>.Instance);

        return new Cenario(anonimo, escopo, obraA, obraB, cameraA, cameraB, agenteA, segredo, analisador, telegram, handler, global);
    }

    private static AnalisarQuadroAgenteCameraCommand Quadro(Cenario c, Guid? cameraId = null, string? segredo = null, DateTime? capturadaEm = null,
        string? versao = null)
        => new(c.AgenteA.Id, segredo ?? c.SegredoA, versao, cameraId ?? c.CameraA.Id, Jpeg, capturadaEm ?? DateTime.UtcNow);

    // ---------- Gestão: criar ----------

    [Fact]
    public async Task Criar_DevolveSegredoUmaVezEGravaSoOHash()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_DevolveSegredoUmaVezEGravaSoOHash));
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        db.Add(obra);
        await db.SaveChangesAsync();

        var registro = await new CriarAgenteCameraCommandHandler(db, Hasher).Handle(new CriarAgenteCameraCommand(obra.Id, "  PC da portaria  "), default);

        var agente = await db.AgentesCamera.SingleAsync();
        Assert.Equal(agente.Id, registro.Id);
        Assert.Equal("PC da portaria", agente.Nome);
        Assert.Equal(obra.Id, agente.ObraId);
        Assert.True(agente.Ativo);
        // Só o hash fica no banco, e é o hash do segredo devolvido.
        Assert.NotEqual(registro.Segredo, agente.SegredoHash);
        Assert.True(Hasher.Verificar(registro.Segredo, agente.SegredoHash));
        // Base64url (seguro em cabeçalho) de 32 bytes aleatórios: 43 caracteres, sem '+', '/' nem '='.
        Assert.Equal(43, registro.Segredo.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", registro.Segredo);
    }

    [Fact]
    public async Task Criar_DoisAgentes_GeramSegredosDiferentes()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_DoisAgentes_GeramSegredosDiferentes));
        var obra = new Obra { Codigo = "OB1", Nome = "Beira Rio" };
        db.Add(obra);
        await db.SaveChangesAsync();
        var handler = new CriarAgenteCameraCommandHandler(db, Hasher);

        var a = await handler.Handle(new CriarAgenteCameraCommand(obra.Id, "A"), default);
        var b = await handler.Handle(new CriarAgenteCameraCommand(obra.Id, "B"), default);

        Assert.NotEqual(a.Segredo, b.Segredo);
    }

    [Fact]
    public async Task Criar_NomeDuplicadoNaObra_Recusa_MasOutraObraEPermitida()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_NomeDuplicadoNaObra_Recusa_MasOutraObraEPermitida));
        var obra1 = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var obra2 = new Obra { Codigo = "O2", Nome = "Obra 2" };
        db.AddRange(obra1, obra2);
        await db.SaveChangesAsync();
        var handler = new CriarAgenteCameraCommandHandler(db, Hasher);
        await handler.Handle(new CriarAgenteCameraCommand(obra1.Id, "PC"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CriarAgenteCameraCommand(obra1.Id, " PC "), default));
        await handler.Handle(new CriarAgenteCameraCommand(obra2.Id, "PC"), default);
    }

    [Fact]
    public async Task Criar_NomeDeAgenteRevogado_PodeSerReaproveitado()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_NomeDeAgenteRevogado_PodeSerReaproveitado));
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        db.Add(obra);
        await db.SaveChangesAsync();
        var criar = new CriarAgenteCameraCommandHandler(db, Hasher);
        var primeiro = await criar.Handle(new CriarAgenteCameraCommand(obra.Id, "PC"), default);
        await new RevogarAgenteCameraCommandHandler(db).Handle(new RevogarAgenteCameraCommand(primeiro.Id), default);

        await criar.Handle(new CriarAgenteCameraCommand(obra.Id, "PC"), default);

        Assert.Equal(2, await db.AgentesCamera.CountAsync());
    }

    [Fact]
    public async Task Criar_ObraInexistente_LancaKeyNotFound()
    {
        var db = CamerasPersistenciaTests.CriarDb(nameof(Criar_ObraInexistente_LancaKeyNotFound));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new CriarAgenteCameraCommandHandler(db, Hasher).Handle(new CriarAgenteCameraCommand(Guid.NewGuid(), "PC"), default));
    }

    [Fact]
    public void Criar_Validador_ExigeObraENomeAte100()
    {
        var validador = new CriarAgenteCameraCommandValidator();
        Assert.False(validador.Validate(new CriarAgenteCameraCommand(Guid.Empty, "PC")).IsValid);
        Assert.False(validador.Validate(new CriarAgenteCameraCommand(Guid.NewGuid(), " ")).IsValid);
        Assert.False(validador.Validate(new CriarAgenteCameraCommand(Guid.NewGuid(), new string('x', 101))).IsValid);
        Assert.True(validador.Validate(new CriarAgenteCameraCommand(Guid.NewGuid(), new string('x', 100))).IsValid);
    }

    // ---------- Gestão: listar e revogar ----------

    [Fact]
    public async Task Listar_NaoExpoeHash_FiltraPorObra_EMostraRevogadoComoInativo()
    {
        var c = await Preparar(nameof(Listar_NaoExpoeHash_FiltraPorObra_EMostraRevogadoComoInativo));
        c.Global.Add(new AgenteCamera { ObraId = c.ObraB.Id, Nome = "PC B", SegredoHash = Hasher.GerarHash("x"), Versao = "1.0.0", UltimoContatoEmUtc = DateTime.UtcNow });
        await c.Global.SaveChangesAsync();
        await new RevogarAgenteCameraCommandHandler(c.Global).Handle(new RevogarAgenteCameraCommand(c.AgenteA.Id), default);
        var handler = new ListarAgentesCameraQueryHandler(c.Global);

        var todos = await handler.Handle(new ListarAgentesCameraQuery(null), default);
        var daObraB = await handler.Handle(new ListarAgentesCameraQuery(c.ObraB.Id), default);

        Assert.Equal(2, todos.Count);
        var revogado = Assert.Single(todos, a => a.Id == c.AgenteA.Id);
        Assert.False(revogado.Ativo);
        Assert.Equal("Obra A", revogado.ObraNome);
        var b = Assert.Single(daObraB);
        Assert.True(b.Ativo);
        Assert.Equal("1.0.0", b.Versao);
        Assert.Equal(DateTimeKind.Utc, b.UltimoContatoEmUtc!.Value.Kind);
        // O DTO não tem campo de hash/segredo.
        Assert.DoesNotContain(typeof(AgenteCameraDto).GetProperties(), p => p.Name.Contains("Segredo", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Listar_UsuarioRestritoAUmaObra_NaoVeAgentesDeOutra()
    {
        var c = await Preparar(nameof(Listar_UsuarioRestritoAUmaObra_NaoVeAgentesDeOutra));
        c.Global.Add(new AgenteCamera { ObraId = c.ObraB.Id, Nome = "PC B", SegredoHash = Hasher.GerarHash("x") });
        await c.Global.SaveChangesAsync();
        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { c.ObraB.Id });
        var db = new SstDbContext(Opcoes(nameof(Listar_UsuarioRestritoAUmaObra_NaoVeAgentesDeOutra)), restrito);

        var lista = await new ListarAgentesCameraQueryHandler(db).Handle(new ListarAgentesCameraQuery(null), default);

        Assert.Equal("PC B", Assert.Single(lista).Nome);
    }

    [Fact]
    public async Task Revogar_MarcaInativoSemApagarORegistro()
    {
        var c = await Preparar(nameof(Revogar_MarcaInativoSemApagarORegistro));

        await new RevogarAgenteCameraCommandHandler(c.Global).Handle(new RevogarAgenteCameraCommand(c.AgenteA.Id), default);

        var agente = await c.Global.AgentesCamera.SingleAsync(a => a.Id == c.AgenteA.Id);
        Assert.False(agente.Ativo);
    }

    [Fact]
    public async Task Revogar_Inexistente_LancaKeyNotFound()
    {
        var c = await Preparar(nameof(Revogar_Inexistente_LancaKeyNotFound));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RevogarAgenteCameraCommandHandler(c.Global).Handle(new RevogarAgenteCameraCommand(Guid.NewGuid()), default));
    }

    // ---------- Autenticação ----------

    [Fact]
    public async Task Autenticar_SegredoCerto_DevolveOAgente_MesmoComEscopoZerado()
    {
        var c = await Preparar(nameof(Autenticar_SegredoCerto_DevolveOAgente_MesmoComEscopoZerado));
        var autenticador = new AgenteCameraAutenticador(c.Anonimo, Hasher);

        var agente = await autenticador.ValidarAsync(c.AgenteA.Id, c.SegredoA, default);

        Assert.Equal(c.AgenteA.Id, agente?.Id);
    }

    [Theory]
    [InlineData("segredo-errado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Autenticar_SegredoErradoOuAusente_DevolveNulo(string? segredo)
    {
        var c = await Preparar("Autenticar_Errado_" + segredo);
        Assert.Null(await new AgenteCameraAutenticador(c.Anonimo, Hasher).ValidarAsync(c.AgenteA.Id, segredo, default));
    }

    [Fact]
    public async Task Autenticar_AgenteRevogado_DevolveNuloMesmoComSegredoCerto()
    {
        var c = await Preparar(nameof(Autenticar_AgenteRevogado_DevolveNuloMesmoComSegredoCerto));
        await new RevogarAgenteCameraCommandHandler(c.Global).Handle(new RevogarAgenteCameraCommand(c.AgenteA.Id), default);

        Assert.Null(await new AgenteCameraAutenticador(c.Anonimo, Hasher).ValidarAsync(c.AgenteA.Id, c.SegredoA, default));
    }

    [Fact]
    public async Task Autenticar_AgenteInexistente_DevolveNulo()
    {
        var c = await Preparar(nameof(Autenticar_AgenteInexistente_DevolveNulo));
        var autenticador = new AgenteCameraAutenticador(c.Anonimo, Hasher);

        Assert.Null(await autenticador.ValidarAsync(Guid.NewGuid(), c.SegredoA, default));
        Assert.Null(await autenticador.ValidarAsync(Guid.Empty, c.SegredoA, default));
    }

    [Fact]
    public void HashDeEquilibrio_EBase64Valido()
        // Se não fosse, Verificar lançaria FormatException para agente inexistente (500 em vez de 401).
        => Assert.False(Hasher.Verificar("qualquer", "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU="));

    [Fact]
    public async Task EnviarQuadro_SegredoDeOutroAgente_RetornaNulo_SemChamarIa()
    {
        var c = await Preparar(nameof(EnviarQuadro_SegredoDeOutroAgente_RetornaNulo_SemChamarIa));

        var r = await c.Handler.Handle(Quadro(c, segredo: Hasher.GerarSegredo()), default);

        Assert.Null(r);
        Assert.Equal(0, c.Analisador.Chamadas);
        // Escopo continua zerado: requisição não autenticada não ganha acesso a obra nenhuma.
        Assert.False(c.Escopo.TemAcessoGlobal);
        Assert.Empty(c.Escopo.ObrasPermitidas);
    }

    [Fact]
    public async Task EnviarQuadro_AgenteRevogado_RetornaNulo()
    {
        var c = await Preparar(nameof(EnviarQuadro_AgenteRevogado_RetornaNulo));
        await new RevogarAgenteCameraCommandHandler(c.Global).Handle(new RevogarAgenteCameraCommand(c.AgenteA.Id), default);

        Assert.Null(await c.Handler.Handle(Quadro(c), default));
        Assert.Equal(0, c.Analisador.Chamadas);
    }

    // ---------- Quadro: escopo, obra e horário ----------

    [Fact]
    public async Task EnviarQuadro_CameraDeOutraObra_LancaKeyNotFound_SemChamarIa()
    {
        var c = await Preparar(nameof(EnviarQuadro_CameraDeOutraObra_LancaKeyNotFound_SemChamarIa));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => c.Handler.Handle(Quadro(c, cameraId: c.CameraB.Id), default));

        Assert.Equal(0, c.Analisador.Chamadas);
        Assert.Empty(await c.Global.OcorrenciasEpiCamera.ToListAsync());
    }

    [Fact]
    public async Task EnviarQuadro_CameraInexistente_LancaKeyNotFound()
    {
        var c = await Preparar(nameof(EnviarQuadro_CameraInexistente_LancaKeyNotFound));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => c.Handler.Handle(Quadro(c, cameraId: Guid.NewGuid()), default));
    }

    // O caso que só quebra em produção: Entra ligado, rota anônima, escopo zerado. Sem DefinirEscopo no handler a
    // câmera some (404 falso), a ocorrência e o trabalhador também.
    [Fact]
    public async Task EnviarQuadro_ComEscopoZeradoDoEntra_EncontraCameraGravaOcorrenciaEIdentificaTrabalhador()
    {
        var banco = nameof(EnviarQuadro_ComEscopoZeradoDoEntra_EncontraCameraGravaOcorrenciaEIdentificaTrabalhador);
        var trabalhador = new Trabalhador { ObraId = Guid.Empty, Nome = "João da Silva" };
        var c = await Preparar(banco, trabalhadorReconhecido: trabalhador.Id);
        trabalhador.ObraId = c.ObraA.Id;
        c.Global.Add(trabalhador);
        await c.Global.SaveChangesAsync();

        // Antes: o contexto anônimo (escopo zerado) não enxerga nada da obra.
        Assert.Empty(await c.Anonimo.Cameras.ToListAsync());
        Assert.Empty(await c.Anonimo.Trabalhadores.ToListAsync());

        var capturada = DateTime.UtcNow.AddMinutes(-42);
        var r = await c.Handler.Handle(Quadro(c, capturadaEm: capturada), default);

        Assert.NotNull(r);
        Assert.Equal(SituacaoQuadro.Ocorrencia, r!.Situacao);
        Assert.Equal(trabalhador.Id, r.TrabalhadorId);
        Assert.Equal("João da Silva", r.TrabalhadorNome);
        Assert.True(r.AlertaEnviado);
        Assert.Equal(1, c.Telegram.Fotos);

        var ocorrencia = await c.Global.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(c.ObraA.Id, ocorrencia.ObraId);
        Assert.Equal(c.CameraA.Id, ocorrencia.CameraId);
        Assert.Equal(trabalhador.Id, ocorrencia.TrabalhadorId);
        // O horário real do quadro (guardado sem internet) chega à ocorrência; o de recebimento é o do servidor.
        Assert.Equal(capturada, ocorrencia.CapturadaEmUtc);
        Assert.True(ocorrencia.RecebidaEmUtc > capturada.AddMinutes(41));
        // Auditoria sem usuário logado: criado por ninguém (nulo), mas com data.
        Assert.Null(ocorrencia.CreatedBy);
        Assert.NotEqual(default, ocorrencia.CreatedAtUtc);
    }

    [Fact]
    public async Task EnviarQuadro_ComEscopoDaObraDoAgente_EnxergaTrabalhadorAreaEObraSoDaPropriaObra()
    {
        var c = await Preparar(nameof(EnviarQuadro_ComEscopoDaObraDoAgente_EnxergaTrabalhadorAreaEObraSoDaPropriaObra));
        var trabalhadorA = new Trabalhador { ObraId = c.ObraA.Id, Nome = "Da obra A" };
        var trabalhadorB = new Trabalhador { ObraId = c.ObraB.Id, Nome = "Da obra B" };
        var areaA = new AreaSst { ObraId = c.ObraA.Id, Nome = "Área A" };
        var areaB = new AreaSst { ObraId = c.ObraB.Id, Nome = "Área B" };
        c.Global.AddRange(trabalhadorA, trabalhadorB, areaA, areaB);
        await c.Global.SaveChangesAsync();
        c.Analisador.Resposta = () => new ResultadoAnaliseEpi(1, 0, 0, ConfiancaAnaliseEpi.Alta); // limpo

        await c.Handler.Handle(Quadro(c), default);

        Assert.Equal(new[] { c.ObraA.Id }, c.Escopo.ObrasPermitidas);
        Assert.False(c.Escopo.TemAcessoGlobal);
        Assert.Equal("Da obra A", Assert.Single(await c.Anonimo.Trabalhadores.ToListAsync()).Nome);
        Assert.Equal("Área A", Assert.Single(await c.Anonimo.AreasSst.ToListAsync()).Nome);
        Assert.Equal("Obra A", Assert.Single(await c.Anonimo.Obras.ToListAsync()).Nome);
        Assert.Equal(c.CameraA.Id, Assert.Single(await c.Anonimo.Cameras.ToListAsync()).Id);
    }

    [Fact]
    public async Task EnviarQuadro_ImagemChegaAoAnalisadorECameraDesabilitadaRecusa()
    {
        var c = await Preparar(nameof(EnviarQuadro_ImagemChegaAoAnalisadorECameraDesabilitadaRecusa));
        var camera = await c.Global.Cameras.SingleAsync(x => x.Id == c.CameraA.Id);
        camera.Habilitada = false;
        await c.Global.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Handler.Handle(Quadro(c), default));
        Assert.Equal(0, c.Analisador.Chamadas);
    }

    // ---------- Contato do agente ----------

    [Fact]
    public async Task EnviarQuadro_RegistraUltimoContatoEVersao()
    {
        var c = await Preparar(nameof(EnviarQuadro_RegistraUltimoContatoEVersao));
        c.Analisador.Resposta = () => new ResultadoAnaliseEpi(1, 0, 0, ConfiancaAnaliseEpi.Alta);

        await c.Handler.Handle(Quadro(c, versao: "1.2.3"), default);

        var agente = await c.Global.AgentesCamera.AsNoTracking().SingleAsync();
        Assert.NotNull(agente.UltimoContatoEmUtc);
        Assert.True(DateTime.UtcNow - agente.UltimoContatoEmUtc!.Value < TimeSpan.FromMinutes(1));
        Assert.Equal("1.2.3", agente.Versao);
    }

    [Fact]
    public async Task EnviarQuadro_ConflitoAoGravarOContato_NaoDerrubaAAnalise()
    {
        var c = await Preparar(nameof(EnviarQuadro_ConflitoAoGravarOContato_NaoDerrubaAAnalise), interceptor: new ConflitoNoAgenteInterceptor());

        var r = await c.Handler.Handle(Quadro(c), default);

        Assert.Equal(SituacaoQuadro.Ocorrencia, r!.Situacao);
        Assert.Single(await c.Global.OcorrenciasEpiCamera.ToListAsync());
        Assert.Null((await c.Global.AgentesCamera.AsNoTracking().SingleAsync()).UltimoContatoEmUtc);
    }

    [Fact]
    public void Validador_RecusaImagemQueNaoEJpegOuMaiorQue4Mb()
    {
        var validador = new AnalisarQuadroAgenteCameraCommandValidator();
        AnalisarQuadroAgenteCameraCommand Com(byte[] imagem) => new(Guid.NewGuid(), "s", null, Guid.NewGuid(), imagem, DateTime.UtcNow);

        Assert.True(validador.Validate(Com(Jpeg)).IsValid);
        Assert.False(validador.Validate(Com(new byte[] { 0x89, 0x50, 0x4E })).IsValid);
        Assert.False(validador.Validate(Com(Array.Empty<byte>())).IsValid);
        var grande = new byte[AnalisarQuadroCameraCommandValidator.TamanhoMaximoBytes + 1];
        grande[0] = 0xFF;
        grande[1] = 0xD8;
        Assert.False(validador.Validate(Com(grande)).IsValid);
    }

    // ---------- Obra inativada ----------

    [Fact]
    public async Task Listar_AgenteDeObraInativada_ContinuaNaListaComONomeDaObra()
    {
        var c = await Preparar(nameof(Listar_AgenteDeObraInativada_ContinuaNaListaComONomeDaObra));
        var obra = await c.Global.Obras.SingleAsync(o => o.Id == c.ObraA.Id);
        obra.Ativo = false;
        await c.Global.SaveChangesAsync();

        var lista = await new ListarAgentesCameraQueryHandler(c.Global).Handle(new ListarAgentesCameraQuery(null), default);

        // Precisa aparecer para poder ser revogado (o autenticador já o recusa: teste abaixo).
        var agente = Assert.Single(lista);
        Assert.Equal(c.AgenteA.Id, agente.Id);
        Assert.Equal("Obra A", agente.ObraNome);
    }

    [Fact]
    public async Task Listar_UsuarioRestritoNaoVeAgenteDeObraInativadaDeOutraObra()
    {
        var c = await Preparar(nameof(Listar_UsuarioRestritoNaoVeAgenteDeObraInativadaDeOutraObra));
        var obra = await c.Global.Obras.SingleAsync(o => o.Id == c.ObraA.Id);
        obra.Ativo = false;
        await c.Global.SaveChangesAsync();
        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { c.ObraB.Id });
        var db = new SstDbContext(Opcoes(nameof(Listar_UsuarioRestritoNaoVeAgenteDeObraInativadaDeOutraObra)), restrito);

        Assert.Empty(await new ListarAgentesCameraQueryHandler(db).Handle(new ListarAgentesCameraQuery(null), default));
    }

    [Fact]
    public async Task Autenticar_ObraInativa_DevolveNuloMesmoComSegredoCerto_ESemEscopoNemIa()
    {
        var c = await Preparar(nameof(Autenticar_ObraInativa_DevolveNuloMesmoComSegredoCerto_ESemEscopoNemIa));
        var obra = await c.Global.Obras.SingleAsync(o => o.Id == c.ObraA.Id);
        obra.Ativo = false;
        await c.Global.SaveChangesAsync();

        Assert.Null(await new AgenteCameraAutenticador(c.Anonimo, Hasher).ValidarAsync(c.AgenteA.Id, c.SegredoA, default));
        Assert.Null(await c.Handler.Handle(Quadro(c), default));
        Assert.Equal(0, c.Analisador.Chamadas);
        Assert.Empty(c.Escopo.ObrasPermitidas);
    }

    [Fact]
    public async Task Autenticar_ReaproveitaOResultadoNaMesmaRequisicao_SoParaOMesmoSegredo()
    {
        var c = await Preparar(nameof(Autenticar_ReaproveitaOResultadoNaMesmaRequisicao_SoParaOMesmoSegredo));
        var autenticador = new AgenteCameraAutenticador(c.Anonimo, Hasher);

        var primeiro = await autenticador.ValidarAsync(c.AgenteA.Id, c.SegredoA, default);
        var segundo = await autenticador.ValidarAsync(c.AgenteA.Id, c.SegredoA, default);

        Assert.Same(primeiro, segundo);
        Assert.Null(await autenticador.ValidarAsync(c.AgenteA.Id, "outro-segredo", default));
    }

    // ---------- Cadastro: corrida no nome ----------

    // Simula o índice único: outro cadastro com o mesmo nome entra entre a checagem e a gravação.
    private sealed class DuplicataNaGravacaoInterceptor : SaveChangesInterceptor
    {
        private readonly DbContextOptions<SstDbContext> _opcoes;
        private readonly Guid _obraId;
        private bool _jaFez;
        public DuplicataNaGravacaoInterceptor(DbContextOptions<SstDbContext> opcoes, Guid obraId) { _opcoes = opcoes; _obraId = obraId; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_jaFez && eventData.Context!.ChangeTracker.Entries<AgenteCamera>().Any(e => e.State == EntityState.Added))
            {
                _jaFez = true;
                using var outro = new SstDbContext(_opcoes, new CurrentUserService());
                outro.AgentesCamera.Add(new AgenteCamera { ObraId = _obraId, Nome = "PC", SegredoHash = "x" });
                await outro.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Violação do índice único (simulada).", new InvalidOperationException("2601"));
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FalhaGenericaNaGravacaoInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AgenteCamera>().Any(e => e.State == EntityState.Added))
                throw new DbUpdateException("Falha qualquer.", new InvalidOperationException("outra"));
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Criar_CorridaNoIndiceUnico_ViraOMesmoErroDeNegocioDoCaminhoNormal()
    {
        var nome = nameof(Criar_CorridaNoIndiceUnico_ViraOMesmoErroDeNegocioDoCaminhoNormal);
        var opcoes = Opcoes(nome);
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        await using (var semear = new SstDbContext(opcoes, new CurrentUserService())) { semear.Add(obra); await semear.SaveChangesAsync(); }
        var db = new SstDbContext(Opcoes(nome, new DuplicataNaGravacaoInterceptor(opcoes, obra.Id)), new CurrentUserService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarAgenteCameraCommandHandler(db, Hasher).Handle(new CriarAgenteCameraCommand(obra.Id, "PC"), default));

        Assert.Contains("Já existe um agente chamado \"PC\"", ex.Message);
        Assert.Equal(1, await db.AgentesCamera.CountAsync());
    }

    [Fact]
    public async Task Criar_FalhaDeGravacaoSemDuplicata_NaoViraErroDeNegocio()
    {
        var nome = nameof(Criar_FalhaDeGravacaoSemDuplicata_NaoViraErroDeNegocio);
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        await using (var semear = new SstDbContext(Opcoes(nome), new CurrentUserService())) { semear.Add(obra); await semear.SaveChangesAsync(); }
        var db = new SstDbContext(Opcoes(nome, new FalhaGenericaNaGravacaoInterceptor()), new CurrentUserService());

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new CriarAgenteCameraCommandHandler(db, Hasher).Handle(new CriarAgenteCameraCommand(obra.Id, "PC"), default));
    }

    // ---------- Quadro atrasado ----------

    private static async Task LiberarCamera(Cenario c)
    {
        // O mínimo de 1 s entre análises da mesma câmera não é o que estes testes verificam.
        var camera = await c.Anonimo.Cameras.SingleAsync(x => x.Id == c.CameraA.Id);
        camera.UltimaAnaliseEmUtc = null;
        await c.Anonimo.SaveChangesAsync();
    }

    private static TimeZoneInfo Brasilia()
        => TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

    private static string EmBrasilia(DateTime utc, string formato)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia()).ToString(formato);

    [Fact]
    public async Task EnviarQuadro_Atrasado_LegendaUsaOHorarioDaCapturaEAvisaDoAtraso_OcorrenciaGuardaOsDoisHorarios()
    {
        var c = await Preparar(nameof(EnviarQuadro_Atrasado_LegendaUsaOHorarioDaCapturaEAvisaDoAtraso_OcorrenciaGuardaOsDoisHorarios));
        var capturada = DateTime.UtcNow.AddMinutes(-47);

        var r = await c.Handler.Handle(Quadro(c, capturadaEm: capturada), default);

        Assert.True(r!.AlertaEnviado);
        var legenda = Assert.Single(c.Telegram.Legendas);
        Assert.Contains($"Registrado com atraso — imagem das {EmBrasilia(capturada, "HH:mm")}", legenda);
        Assert.Contains($"Câmera Betoneira · {EmBrasilia(capturada, "dd/MM HH:mm")}", legenda);
        var o = await c.Global.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(capturada, o.CapturadaEmUtc);
        Assert.Equal(capturada, o.UltimaVezEmUtc);
        Assert.True(DateTime.UtcNow - o.RecebidaEmUtc < TimeSpan.FromMinutes(1)); // recebimento = relógio do servidor
        // Alerta de quadro atrasado não ocupa o intervalo entre alertas da câmera.
        Assert.Null((await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.CameraA.Id)).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task EnviarQuadro_DentroDaTolerancia_LegendaSemAvisoDeAtraso()
    {
        var c = await Preparar(nameof(EnviarQuadro_DentroDaTolerancia_LegendaSemAvisoDeAtraso));

        await c.Handler.Handle(Quadro(c, capturadaEm: DateTime.UtcNow.AddSeconds(-90)), default);

        Assert.DoesNotContain("atraso", Assert.Single(c.Telegram.Legendas));
        Assert.NotNull((await c.Global.Cameras.AsNoTracking().SingleAsync(x => x.Id == c.CameraA.Id)).UltimoAlertaEmUtc);
    }

    [Fact]
    public async Task EnviarQuadro_AtrasadoDeOutraEpoca_NaoBloqueiaNemSomaNoAlertaAoVivoSemIdentificacao()
    {
        var c = await Preparar(nameof(EnviarQuadro_AtrasadoDeOutraEpoca_NaoBloqueiaNemSomaNoAlertaAoVivoSemIdentificacao));
        await c.Handler.Handle(Quadro(c, capturadaEm: DateTime.UtcNow.AddHours(-3)), default);
        await LiberarCamera(c);

        var aoVivo = await c.Handler.Handle(Quadro(c), default);

        Assert.False(aoVivo!.Repetida);
        Assert.True(aoVivo.AlertaEnviado);
        Assert.Equal(2, c.Telegram.Fotos);
        Assert.Equal(2, await c.Global.OcorrenciasEpiCamera.CountAsync());
    }

    [Fact]
    public async Task EnviarQuadro_AtrasadoDeOutraEpoca_NaoConsomeACotaDeAlertaDoTrabalhadorAoVivo()
    {
        var trabalhador = new Trabalhador { ObraId = Guid.Empty, Nome = "João" };
        var c = await Preparar(nameof(EnviarQuadro_AtrasadoDeOutraEpoca_NaoConsomeACotaDeAlertaDoTrabalhadorAoVivo), trabalhadorReconhecido: trabalhador.Id);
        trabalhador.ObraId = c.ObraA.Id;
        c.Global.Add(trabalhador);
        await c.Global.SaveChangesAsync();

        await c.Handler.Handle(Quadro(c, capturadaEm: DateTime.UtcNow.AddHours(-3)), default);
        await LiberarCamera(c);
        var aoVivo = await c.Handler.Handle(Quadro(c), default);

        Assert.True(aoVivo!.AlertaEnviado);
        Assert.False(aoVivo.Repetida);
        Assert.Equal(2, c.Telegram.Fotos);
        Assert.Equal(2, await c.Global.OcorrenciasEpiCamera.CountAsync());
    }

    [Fact]
    public async Task EnviarQuadro_AtrasadoDaMesmaEpocaDoMesmoTrabalhador_AgrupaERespeitaACota()
    {
        var trabalhador = new Trabalhador { ObraId = Guid.Empty, Nome = "João" };
        var c = await Preparar(nameof(EnviarQuadro_AtrasadoDaMesmaEpocaDoMesmoTrabalhador_AgrupaERespeitaACota), trabalhadorReconhecido: trabalhador.Id);
        trabalhador.ObraId = c.ObraA.Id;
        c.Global.Add(trabalhador);
        await c.Global.SaveChangesAsync();

        await c.Handler.Handle(Quadro(c, capturadaEm: DateTime.UtcNow.AddHours(-3)), default);
        await LiberarCamera(c);
        var r = await c.Handler.Handle(Quadro(c, capturadaEm: DateTime.UtcNow.AddHours(-3).AddMinutes(20)), default);

        Assert.True(r!.Repetida);
        Assert.False(r.AlertaEnviado);
        Assert.Equal(1, c.Telegram.Fotos);
        Assert.Equal(1, await c.Global.OcorrenciasEpiCamera.CountAsync());
    }

    [Fact]
    public async Task EnviarQuadro_DoisAtrasadosDaMesmaCenaSemIdentificacao_SomamNumaOcorrenciaSo()
    {
        var c = await Preparar(nameof(EnviarQuadro_DoisAtrasadosDaMesmaCenaSemIdentificacao_SomamNumaOcorrenciaSo));
        var t = DateTime.UtcNow.AddMinutes(-40);

        await c.Handler.Handle(Quadro(c, capturadaEm: t), default);
        await LiberarCamera(c);
        var r = await c.Handler.Handle(Quadro(c, capturadaEm: t.AddMinutes(1)), default);

        Assert.True(r!.Repetida);
        var o = await c.Global.OcorrenciasEpiCamera.SingleAsync();
        Assert.Equal(2, o.VezesVista);
        Assert.Equal(t.AddMinutes(1), o.UltimaVezEmUtc);
        Assert.Equal(t, o.CapturadaEmUtc);
    }

    // O técnico edita a ocorrência no instante em que o alerta é marcado como enviado: o conflito não pode fazer o
    // alerta sair duas vezes (o agente reenviaria o quadro) nem derrubar a resposta.
    private sealed class ConflitoUmaVezNoAlertaInterceptor : SaveChangesInterceptor
    {
        public int Conflitos { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Conflitos == 0 && eventData.Context!.ChangeTracker.Entries<OcorrenciaEpiCamera>().Any(e => e.State == EntityState.Modified && e.Entity.AlertaEnviado))
            {
                Conflitos++;
                throw new DbUpdateConcurrencyException("Conflito simulado na ocorrência.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task EnviarQuadro_ConflitoAoMarcarAlertaEnviado_TentaDeNovo_SemReenviarOAlerta()
    {
        var interceptor = new ConflitoUmaVezNoAlertaInterceptor();
        var c = await Preparar(nameof(EnviarQuadro_ConflitoAoMarcarAlertaEnviado_TentaDeNovo_SemReenviarOAlerta), interceptor: interceptor);

        var r = await c.Handler.Handle(Quadro(c), default);

        Assert.Equal(1, interceptor.Conflitos);
        Assert.True(r!.AlertaEnviado);
        Assert.Equal(1, c.Telegram.Fotos);
        Assert.True((await c.Global.OcorrenciasEpiCamera.AsNoTracking().SingleAsync()).AlertaEnviado);
    }

    [Fact]
    public async Task ComandoDoNavegador_QuadroAntigoContinuaComHorarioDeAgoraNaLegenda()
    {
        var c = await Preparar(nameof(ComandoDoNavegador_QuadroAntigoContinuaComHorarioDeAgoraNaLegenda));
        var interno = new AnalisarQuadroCameraCommandHandler(c.Anonimo, c.Analisador, c.Telegram, new FacialFalso(null),
            NullLogger<AnalisarQuadroCameraCommandHandler>.Instance);
        c.Escopo.DefinirEscopo(false, new[] { c.ObraA.Id });

        await interno.Handle(new AnalisarQuadroCameraCommand(c.CameraA.Id, Jpeg, DateTime.UtcNow.AddHours(-3)), default);

        var legenda = Assert.Single(c.Telegram.Legendas);
        Assert.DoesNotContain("atraso", legenda);
        Assert.Contains(EmBrasilia(DateTime.UtcNow, "dd/MM HH:mm"), legenda);
    }
}
