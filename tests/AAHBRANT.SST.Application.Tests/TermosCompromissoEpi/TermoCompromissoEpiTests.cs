using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.TermosCompromissoEpi;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.TermosCompromissoEpi;

// Termo de Recebimento e Compromisso de Uso (03/10): o sistema nunca fabrica assinatura eletrônica de
// quem assinou em papel, e o termo só vale se o próprio funcionário assinar (digital/facial).
public class TermoCompromissoEpiTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.AddHours(-3).Date;

    private static async Task<(Trabalhador trabalhador, Usuario usuario)> CriarCenarioAsync(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db)
    {
        var trabalhador = new Trabalhador { Nome = "João da Silva", Cpf = "11122233344", Funcao = new Funcao { Nome = "Pedreiro" } };
        var usuario = new Usuario { Nome = "Carlos Técnico", Email = "carlos@exemplo.com" };
        db.Trabalhadores.Add(trabalhador);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (trabalhador, usuario);
    }

    [Fact]
    public async Task RegistrarManual_GravaERefleteComoManualNaConsulta()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, usuario) = await CriarCenarioAsync(db);
        var handler = new RegistrarTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa());

        await handler.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje.AddDays(-30), "Papel arquivado na pasta 3", usuario.Id), CancellationToken.None);

        var termo = await TermoCompromissoEpiConsulta.ObterAsync(db, trabalhador.Id, CancellationToken.None);
        Assert.Equal(SituacaoTermoCompromissoEpi.Manual, termo.Situacao);
        Assert.Equal(Hoje.AddDays(-30), termo.DataAssinatura);
        Assert.Equal("Carlos Técnico", termo.RegistradoPorNome);
        Assert.Equal("Papel arquivado na pasta 3", termo.Observacao);
        Assert.False(termo.TemArquivo);
        Assert.Null(termo.Metodo);
    }

    [Fact]
    public async Task RegistrarManual_SegundoRegistroDoMesmoFuncionario_ERecusado()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, usuario) = await CriarCenarioAsync(db);
        var handler = new RegistrarTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa());
        await handler.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None));
    }

    [Fact]
    public async Task RegistrarManual_QuandoJaAssinouDigitalmente_ERecusado()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, usuario) = await CriarCenarioAsync(db);
        var documento = new DocumentoAssinatura { EntidadeTipo = TermoCompromissoEpiConsulta.EntidadeTipo, EntidadeId = trabalhador.Id };
        db.DocumentosAssinatura.Add(documento);
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new RegistrarTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None));
        Assert.Contains("digitalmente", ex.Message);
        Assert.Equal(SituacaoTermoCompromissoEpi.Digital, (await TermoCompromissoEpiConsulta.ObterAsync(db, trabalhador.Id, CancellationToken.None)).Situacao);
    }

    [Fact]
    public async Task Remover_VoltaOTermoParaPendenteEPermiteRegistrarDeNovo()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, usuario) = await CriarCenarioAsync(db);
        var registrar = new RegistrarTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa());
        await registrar.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None);

        await new RemoverTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa())
            .Handle(new RemoverTermoManualEpiCommand(trabalhador.Id, usuario.Id), CancellationToken.None);

        Assert.Equal(SituacaoTermoCompromissoEpi.Pendente, (await TermoCompromissoEpiConsulta.ObterAsync(db, trabalhador.Id, CancellationToken.None)).Situacao);
        await registrar.Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None);
        Assert.Equal(SituacaoTermoCompromissoEpi.Manual, (await TermoCompromissoEpiConsulta.ObterAsync(db, trabalhador.Id, CancellationToken.None)).Situacao);
    }

    [Fact]
    public async Task Motor_RecusaAssinaturaPorSessaoLogadaNoTermo()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, _) = await CriarCenarioAsync(db);
        var documento = new DocumentoAssinatura { EntidadeTipo = TermoCompromissoEpiConsulta.EntidadeTipo, EntidadeId = trabalhador.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, CancellationToken.None));

        Assert.Contains("próprio funcionário", ex.Message);
        Assert.Empty(db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == documento.Id));
    }

    [Fact]
    public async Task Motor_RecusaAssinaturaDigitalQuandoOTermoJaFoiRegistradoEmPapel()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, usuario) = await CriarCenarioAsync(db);
        await new RegistrarTermoManualEpiCommandHandler(db, new AuditoriaServiceFalsa())
            .Handle(new RegistrarTermoManualEpiCommand(trabalhador.Id, Hoje, null, usuario.Id), CancellationToken.None);
        var documento = new DocumentoAssinatura { EntidadeTipo = TermoCompromissoEpiConsulta.EntidadeTipo, EntidadeId = trabalhador.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria), null, CancellationToken.None));

        Assert.Contains("papel", ex.Message);
        Assert.Empty(db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == documento.Id));
    }

    [Fact]
    public async Task Motor_AceitaAssinaturaDigitalDoProprioFuncionarioNoTermo()
    {
        using var db = DbContextFactory.Criar();
        var (trabalhador, _) = await CriarCenarioAsync(db);
        var documento = new DocumentoAssinatura { EntidadeTipo = TermoCompromissoEpiConsulta.EntidadeTipo, EntidadeId = trabalhador.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial), null, CancellationToken.None);

        var termo = await TermoCompromissoEpiConsulta.ObterAsync(db, trabalhador.Id, CancellationToken.None);
        Assert.Equal(SituacaoTermoCompromissoEpi.Digital, termo.Situacao);
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, termo.Metodo);
    }

    [Fact]
    public void Validador_RecusaDataFuturaEAceitaDataPassada()
    {
        var validador = new RegistrarTermoManualEpiCommandValidator();
        var id = Guid.NewGuid();

        Assert.False(validador.Validate(new RegistrarTermoManualEpiCommand(id, Hoje.AddDays(1), null, id)).IsValid);
        Assert.True(validador.Validate(new RegistrarTermoManualEpiCommand(id, Hoje, null, id)).IsValid);
        Assert.True(validador.Validate(new RegistrarTermoManualEpiCommand(id, Hoje.AddYears(-1), "ok", id)).IsValid);
    }

    [Fact]
    public void Validador_ArquivoComTipoNaoPermitidoOuAssinaturaFalsa_ERecusado()
    {
        var validador = new RegistrarTermoManualEpiCommandValidator();
        var id = Guid.NewGuid();
        var texto = System.Text.Encoding.UTF8.GetBytes("isto não é um pdf");

        Assert.False(validador.Validate(new RegistrarTermoManualEpiCommand(id, Hoje, null, id, "termo.exe", texto, "application/x-msdownload")).IsValid);
        Assert.False(validador.Validate(new RegistrarTermoManualEpiCommand(id, Hoje, null, id, "termo.pdf", texto, "application/pdf")).IsValid);
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(
            string acao,
            string entidadeTipo,
            Guid entidadeId,
            Guid? usuarioId,
            Guid? trabalhadorId,
            object? dadosDepois,
            CancellationToken ct) => Task.CompletedTask;
    }
}
