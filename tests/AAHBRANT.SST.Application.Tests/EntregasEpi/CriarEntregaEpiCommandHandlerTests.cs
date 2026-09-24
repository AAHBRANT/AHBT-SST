using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using AAHBRANT.SST.Application.Tests.TestSupport;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class CriarEntregaEpiCommandHandlerTests
{
    // Estes testes gravam um Trabalhador, e o conversor de CPF exige chaves em
    // CpfCriptografiaContexto (normalmente configuradas por AddInfrastructure, que este projeto de
    // teste não executa). Sem isto a classe só passava quando alguma outra classe de teste rodava
    // antes e configurava o estático — rodar só esta classe pelo filtro falhava com "Chave de hash
    // do CPF não configurada".
    static CriarEntregaEpiCommandHandlerTests()
    {
        ChavesCpfDeTeste.Configurar();
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Trabalhador trabalhador, CatalogoEpi catalogo)> CriarTrabalhadorECatalogoAsync(IAppDbContext db, string nomeFuncao = "Pedreiro")
    {
        var funcao = new Funcao { Nome = nomeFuncao };
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), Funcao = funcao, Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var catalogo = new CatalogoEpi { Nome = "Capacete de Segurança", VidaUtilEmMeses = 12 };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoEpis.Add(catalogo);
        db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = catalogo.Id, ObraId = trabalhador.ObraId, Saldo = 10 });
        await db.SaveChangesAsync();
        return (trabalhador, catalogo);
    }

    private static CriarEntregaEpiCommand ComandoPara(Trabalhador trabalhador, CatalogoEpi catalogo) => new(
        trabalhador.Id, catalogo.Id, DateTime.UtcNow, null, null, 1, null, null, null,
        MotivoEntregaEpi.Inicial, null, null);

    [Fact]
    public async Task Handle_SemCursoDeIntegracaoConfigurado_CriaEntregaNormalmente()
    {
        var db = CriarDb(nameof(Handle_SemCursoDeIntegracaoConfigurado_CriaEntregaNormalmente));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Handle_ComCursoIntegracao_SemTreinamentoRegistrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_ComCursoIntegracao_SemTreinamentoRegistrado_LancaInvalidOperationException));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        db.CursosTreinamento.Add(new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ComandoPara(trabalhador, catalogo), default));
        Assert.Contains("Integração de Segurança", ex.Message);
    }

    [Fact]
    public async Task Handle_TecnicoSegurancaSemIntegracao_CriaEntregaNormalmente()
    {
        var db = CriarDb(nameof(Handle_TecnicoSegurancaSemIntegracao_CriaEntregaNormalmente));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, "Técnico de Segurança");
        db.CursosTreinamento.Add(new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Handle_TreinamentoRegistradoMasNaoAssinadoPeloTrabalhador_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TreinamentoRegistradoMasNaoAssinadoPeloTrabalhador_LancaInvalidOperationException));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddDays(-1),
            DataValidade = DateTime.UtcNow.AddMonths(6),
            CargaHorariaRealizada = 4,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ComandoPara(trabalhador, catalogo), default));
        Assert.Contains("ainda não o assinou", ex.Message);
    }

    // Lançamento retroativo (decisão do usuário, 23/09): obra já em andamento tem gente treinada
    // antes de o sistema existir, e a assinatura eletrônica desse treinamento nunca vai existir
    // aqui. O certificado externo COM arquivo anexado vale como prova no lugar dela.
    [Fact]
    public async Task Handle_CertificadoExternoComArquivoAnexado_CriaEntregaNormalmente()
    {
        var db = CriarDb(nameof(Handle_CertificadoExternoComArquivoAnexado_CriaEntregaNormalmente));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        var treinamento = new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-3),
            DataValidade = DateTime.UtcNow.AddMonths(9),
            CargaHorariaRealizada = 4,
            OrigemCertificado = OrigemCertificadoTreinamento.Externo,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        db.ArquivosCertificadoTreinamento.Add(new ArquivoCertificadoTreinamento
        {
            TreinamentoId = treinamento.Id,
            NomeArquivo = "integracao-2026.pdf",
            ContentType = "application/pdf",
            Conteudo = new byte[] { 1, 2, 3 },
            TamanhoBytes = 3,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    // Sem o arquivo anexado sobraria só a palavra de quem digitou — e é justamente o documento que
    // a fiscalização pede. Continua bloqueando.
    [Fact]
    public async Task Handle_CertificadoExternoSemArquivoAnexado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_CertificadoExternoSemArquivoAnexado_LancaInvalidOperationException));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-3),
            DataValidade = DateTime.UtcNow.AddMonths(9),
            CargaHorariaRealizada = 4,
            OrigemCertificado = OrigemCertificadoTreinamento.Externo,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ComandoPara(trabalhador, catalogo), default));
        Assert.Contains("ainda não o assinou", ex.Message);
    }

    // Assinatura só do instrutor (SessaoLogada) não conta — precisa ser o próprio trabalhador
    // (Biometria/ReconhecimentoFacial), senão o registro só prova que alguém ministrou o curso, não
    // que este trabalhador específico o recebeu.
    [Fact]
    public async Task Handle_TreinamentoSoAssinadoPeloInstrutor_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TreinamentoSoAssinadoPeloInstrutor_LancaInvalidOperationException));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        var treinamento = new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddDays(-1),
            DataValidade = DateTime.UtcNow.AddMonths(6),
            CargaHorariaRealizada = 4,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = Guid.NewGuid(),
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ComandoPara(trabalhador, catalogo), default));
    }

    [Fact]
    public async Task Handle_TreinamentoAssinadoPeloTrabalhador_CriaEntregaNormalmente()
    {
        var db = CriarDb(nameof(Handle_TreinamentoAssinadoPeloTrabalhador_CriaEntregaNormalmente));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        var treinamento = new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddDays(-1),
            DataValidade = DateTime.UtcNow.AddMonths(6),
            CargaHorariaRealizada = 4,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Handle_TreinamentoAssinadoPorReconhecimentoFacial_CriaEntregaNormalmente()
    {
        var db = CriarDb(nameof(Handle_TreinamentoAssinadoPorReconhecimentoFacial_CriaEntregaNormalmente));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        var treinamento = new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddDays(-1),
            DataValidade = DateTime.UtcNow.AddMonths(6),
            CargaHorariaRealizada = 4,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Handle_TreinamentoIntegracaoVencido_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TreinamentoIntegracaoVencido_LancaInvalidOperationException));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        var curso = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        var treinamento = new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddYears(-2),
            DataValidade = DateTime.UtcNow.AddDays(-1),
            CargaHorariaRealizada = 4,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        db.DocumentosAssinatura.Add(new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ComandoPara(trabalhador, catalogo), default));
    }
}
