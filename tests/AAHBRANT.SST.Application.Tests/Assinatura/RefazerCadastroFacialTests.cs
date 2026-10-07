using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Refazer o cadastro facial (pedido do usuário, 07/10): apaga Azure + fotos, audita e libera nova captura.
public class RefazerCadastroFacialTests
{
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        public bool Falhar { get; init; }
        public List<Guid> Removidos { get; } = new();

        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;

        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct)
        {
            if (Falhar) throw new InvalidOperationException("Azure fora do ar.");
            Removidos.Add(trabalhadorId);
            return Task.CompletedTask;
        }

        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public List<(string Acao, Guid EntidadeId, Guid? UsuarioId, object? Dados)> Registros { get; } = new();

        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        {
            Registros.Add((acao, entidadeId, usuarioId, dadosDepois));
            return Task.CompletedTask;
        }
    }

    private sealed class UsuarioFalso : ICurrentUserService
    {
        public bool TemAcessoGlobal { get; private set; } = true;
        public IReadOnlyList<Guid> ObrasPermitidas { get; private set; } = Array.Empty<Guid>();
        public void DefinirEscopo(bool temAcessoGlobal, IReadOnlyList<Guid> obrasPermitidas)
        {
            TemAcessoGlobal = temAcessoGlobal;
            ObrasPermitidas = obrasPermitidas;
        }
    }

    private static RefazerCadastroFacialCommandHandler Handler(IAppDbContext db, IAutenticacaoFacialService facial, IAuditoriaService auditoria, ICurrentUserService? usuario = null)
        => new(db, facial, auditoria, usuario ?? new UsuarioFalso());

    private static async Task<Trabalhador> CriarComFacialAsync(IAppDbContext db, int fotos = 2)
    {
        var trabalhador = new Trabalhador { Nome = "Fulano", AzureFacePersonId = "person-1" };
        db.Trabalhadores.Add(trabalhador);
        for (var i = 0; i < fotos; i++)
            db.FotosCadastroFacial.Add(new FotoCadastroFacial
            {
                TrabalhadorId = trabalhador.Id,
                Conteudo = new byte[] { (byte)i },
                ContentType = "image/jpeg",
                HashSha256 = new string((char)('a' + i), 64),
                CapturadaEm = new DateTime(2026, 10, 2, 14, i, 0, DateTimeKind.Utc),
            });
        await db.SaveChangesAsync();
        return trabalhador;
    }

    [Fact]
    public async Task Refazer_ApagaFotosELimpaPersonIdEAudita()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarComFacialAsync(db);
        var facial = new FacialFalso();
        var auditoria = new AuditoriaFalsa();
        var usuarioId = Guid.NewGuid();

        await Handler(db, facial, auditoria)
            .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, usuarioId, "  foto desfocada  "), CancellationToken.None);

        Assert.Equal(new[] { trabalhador.Id }, facial.Removidos);
        Assert.Equal(0, await db.FotosCadastroFacial.CountAsync(f => f.TrabalhadorId == trabalhador.Id));
        Assert.Null((await db.Trabalhadores.SingleAsync(t => t.Id == trabalhador.Id)).AzureFacePersonId);

        var registro = Assert.Single(auditoria.Registros);
        Assert.Equal("Trabalhador.CadastroFacialRefeito", registro.Acao);
        Assert.Equal(usuarioId, registro.UsuarioId);
        var json = System.Text.Json.JsonSerializer.Serialize(registro.Dados);
        Assert.Contains("foto desfocada", json);
        Assert.Contains(new string('a', 64), json);
        Assert.DoesNotContain("Conteudo", json);
    }

    [Fact]
    public async Task Refazer_SeAzureFalha_NaoApagaNadaLocalENaoAudita()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarComFacialAsync(db);
        var auditoria = new AuditoriaFalsa();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, new FacialFalso { Falhar = true }, auditoria)
                .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, Guid.NewGuid(), "foto ruim"), CancellationToken.None));

        Assert.Equal(2, await db.FotosCadastroFacial.CountAsync(f => f.TrabalhadorId == trabalhador.Id));
        Assert.Equal("person-1", (await db.Trabalhadores.SingleAsync(t => t.Id == trabalhador.Id)).AzureFacePersonId);
        Assert.Empty(auditoria.Registros);
    }

    [Fact]
    public async Task Refazer_ArquivaAsFotos_EOLogAindaAsEnxergaPeloHistorico()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarComFacialAsync(db, fotos: 1);

        await Handler(db, new FacialFalso(), new AuditoriaFalsa())
            .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, Guid.NewGuid(), "foto desfocada"), CancellationToken.None);

        // Fora do cadastro vigente...
        Assert.Equal(0, await db.FotosCadastroFacial.CountAsync(f => f.TrabalhadorId == trabalhador.Id));
        // ...mas o histórico (usado pelo log de assinaturas de EPI) continua com a foto e o hash.
        var arquivada = await db.FotosCadastroFacial.IgnoreQueryFilters().SingleAsync(f => f.TrabalhadorId == trabalhador.Id);
        Assert.False(arquivada.Ativo);
        Assert.NotEmpty(arquivada.Conteudo);
    }

    [Fact]
    public async Task Refazer_TrabalhadorDeOutraObra_EhNaoEncontrado()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarComFacialAsync(db);
        trabalhador.ObraId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var usuario = new UsuarioFalso();
        usuario.DefinirEscopo(false, new[] { Guid.NewGuid() });
        var facial = new FacialFalso();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Handler(db, facial, new AuditoriaFalsa(), usuario)
                .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, Guid.NewGuid(), "foto desfocada"), CancellationToken.None));

        Assert.Empty(facial.Removidos);
        Assert.Equal(2, await db.FotosCadastroFacial.CountAsync(f => f.TrabalhadorId == trabalhador.Id));
    }

    [Fact]
    public async Task Refazer_TrabalhadorDaObraDoUsuario_EhPermitido()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = await CriarComFacialAsync(db);
        var usuario = new UsuarioFalso();
        usuario.DefinirEscopo(false, new[] { trabalhador.ObraId });
        var facial = new FacialFalso();

        await Handler(db, facial, new AuditoriaFalsa(), usuario)
            .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, Guid.NewGuid(), "foto desfocada"), CancellationToken.None);

        Assert.Equal(new[] { trabalhador.Id }, facial.Removidos);
    }

    [Fact]
    public async Task Refazer_SemCadastroFacial_EhRecusado()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Sem facial" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        var facial = new FacialFalso();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, facial, new AuditoriaFalsa())
                .Handle(new RefazerCadastroFacialCommand(trabalhador.Id, Guid.NewGuid(), "qualquer motivo"), CancellationToken.None));

        Assert.Contains("não tem cadastro facial", erro.Message);
        Assert.Empty(facial.Removidos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Validador_RecusaMotivoVazioOuCurto(string motivo)
    {
        var resultado = new RefazerCadastroFacialCommandValidator()
            .Validate(new RefazerCadastroFacialCommand(Guid.NewGuid(), Guid.NewGuid(), motivo));
        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Validador_AceitaMotivoNormal()
    {
        var resultado = new RefazerCadastroFacialCommandValidator()
            .Validate(new RefazerCadastroFacialCommand(Guid.NewGuid(), Guid.NewGuid(), "foto desfocada"));
        Assert.True(resultado.IsValid);
    }
}
