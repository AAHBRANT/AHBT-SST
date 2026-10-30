using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

// LGPD (12/10): desligado, excluído ou com consentimento revogado não fica com biometria ativa.
public class BiometriaDesligamentoERevogacaoTests
{
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        public List<Guid> Removidos { get; } = new();
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) { Removidos.Add(trabalhadorId); return Task.CompletedTask; }
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => Task.CompletedTask;
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => Task.FromResult<ResultadoMigracaoModeloFacial?>(null);
        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false)
            => throw new NotSupportedException();
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Trabalhador Trabalhador)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow,
            AzureFacePersonId = "person-1", ConsentimentoBiometriaEm = DateTime.UtcNow, ConsentimentoBiometriaVersao = "v1",
        };
        db.Trabalhadores.Add(trabalhador);
        db.FotosCadastroFacial.Add(new FotoCadastroFacial { TrabalhadorId = trabalhador.Id, Conteudo = new byte[] { 1 }, ContentType = "image/jpeg", HashSha256 = "h", CapturadaEm = DateTime.UtcNow });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhador.Id, TemplateCriptografado = "x", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, trabalhador);
    }

    [Fact]
    public async Task Revogar_ApagaConsentimentoRemoveDoAzureEArquivaFotosEDigitais()
    {
        var (db, trabalhador) = await PrepararAsync();
        var facial = new FacialFalso();

        await new RevogarConsentimentoBiometriaCommandHandler(db, facial, new AuditoriaFalsa(), NullLogger<RevogarConsentimentoBiometriaCommandHandler>.Instance)
            .Handle(new RevogarConsentimentoBiometriaCommand(trabalhador.Id, "Pedido do trabalhador"), default);

        var salvo = await db.Trabalhadores.IgnoreQueryFilters().SingleAsync(t => t.Id == trabalhador.Id);
        Assert.Null(salvo.ConsentimentoBiometriaEm);
        Assert.NotNull(salvo.ConsentimentoBiometriaRevogadoEm);
        Assert.Null(salvo.AzureFacePersonId);
        Assert.Equal(new[] { trabalhador.Id }, facial.Removidos);
        Assert.False(await db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == trabalhador.Id));
        Assert.False(await db.TemplatesBiometricoFutronic.AnyAsync(t => t.TrabalhadorId == trabalhador.Id));
        // Arquivadas, não apagadas: a foto continua como prova das assinaturas antigas.
        Assert.True(await db.FotosCadastroFacial.IgnoreQueryFilters().AnyAsync(f => f.TrabalhadorId == trabalhador.Id));
    }

    [Fact]
    public async Task Excluir_RemoveABiometria()
    {
        var (db, trabalhador) = await PrepararAsync();
        var facial = new FacialFalso();

        await new ExcluirTrabalhadorCommandHandler(db, facial).Handle(new ExcluirTrabalhadorCommand(trabalhador.Id), default);

        Assert.Equal(new[] { trabalhador.Id }, facial.Removidos);
        Assert.False(await db.TemplatesBiometricoFutronic.AnyAsync(t => t.TrabalhadorId == trabalhador.Id));
    }
}
