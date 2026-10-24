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

    // Treinamento de NR-06 válido por padrão: sem ele a entrega é barrada (trava no servidor).
    private static async Task<CursoTreinamento> CriarCursoNr6Async(IAppDbContext db)
    {
        var curso = new CursoTreinamento { Nome = "NR-06 Uso de EPI", NormaReferencia = "NR-06", AtendeNr6 = true };
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        return curso;
    }

    private static async Task<Treinamento> AdicionarTreinamentoNr6Async(
        IAppDbContext db, Guid trabalhadorId, CursoTreinamento curso, DateTime realizacao, DateTime validade, string? numero = "LP-001")
    {
        var t = new Treinamento
        {
            TrabalhadorId = trabalhadorId,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = realizacao,
            DataValidade = validade,
            NumeroCertificado = numero,
        };
        db.Treinamentos.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    private static async Task<(Trabalhador trabalhador, CatalogoEpi catalogo)> CriarTrabalhadorECatalogoAsync(
        IAppDbContext db, string nomeFuncao = "Pedreiro", bool comNr6 = true)
    {
        var funcao = new Funcao { Nome = nomeFuncao };
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), Funcao = funcao, Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var catalogo = new CatalogoEpi { Nome = "Capacete de Segurança", VidaUtilEmMeses = 12 };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoEpis.Add(catalogo);
        db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = catalogo.Id, ObraId = trabalhador.ObraId, Saldo = 10 });
        await db.SaveChangesAsync();
        if (comNr6)
        {
            var curso = await CriarCursoNr6Async(db);
            await AdicionarTreinamentoNr6Async(db, trabalhador.Id, curso, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddYears(1));
        }
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
    public async Task Handle_ComCursoIntegracao_SemTreinamentoRegistrado_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida()
    {
        var db = CriarDb(nameof(Handle_ComCursoIntegracao_SemTreinamentoRegistrado_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db);
        db.CursosTreinamento.Add(new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, CargaHorariaMinima = 4, ValidadeEmMeses = 12 });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);
        // 02/10: só a NR-06 é exigida para entregar EPI; a Integração de Segurança não bloqueia mais.
        Assert.NotEqual(Guid.Empty, id);
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
    public async Task Handle_TreinamentoRegistradoMasNaoAssinadoPeloTrabalhador_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida()
    {
        var db = CriarDb(nameof(Handle_TreinamentoRegistradoMasNaoAssinadoPeloTrabalhador_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida));
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

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);
        // 02/10: só a NR-06 é exigida para entregar EPI; a Integração de Segurança não bloqueia mais.
        Assert.NotEqual(Guid.Empty, id);
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
    public async Task Handle_CertificadoExternoSemArquivoAnexado_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida()
    {
        var db = CriarDb(nameof(Handle_CertificadoExternoSemArquivoAnexado_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida));
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

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);
        // 02/10: só a NR-06 é exigida para entregar EPI; a Integração de Segurança não bloqueia mais.
        Assert.NotEqual(Guid.Empty, id);
    }

    // Assinatura só do instrutor (SessaoLogada) não conta — precisa ser o próprio trabalhador
    // (Biometria/ReconhecimentoFacial), senão o registro só prova que alguém ministrou o curso, não
    // que este trabalhador específico o recebeu.
    [Fact]
    public async Task Handle_TreinamentoSoAssinadoPeloInstrutor_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida()
    {
        var db = CriarDb(nameof(Handle_TreinamentoSoAssinadoPeloInstrutor_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida));
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

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);
        // 02/10: só a NR-06 é exigida para entregar EPI; a Integração de Segurança não bloqueia mais.
        Assert.NotEqual(Guid.Empty, id);
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
    public async Task Handle_TreinamentoIntegracaoVencido_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida()
    {
        var db = CriarDb(nameof(Handle_TreinamentoIntegracaoVencido_CriaEntregaNormalmente_IntegracaoNaoEMaisExigida));
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

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);
        // 02/10: só a NR-06 é exigida para entregar EPI; a Integração de Segurança não bloqueia mais.
        Assert.NotEqual(Guid.Empty, id);
    }

    // Trava de NR-06 no servidor (auditoria 06/10/2026, A1).
    [Fact]
    public async Task Handle_SemTreinamentoNr6_Bloqueia()
    {
        var db = CriarDb(nameof(Handle_SemTreinamentoNr6_Bloqueia));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, comNr6: false);
        var handler = new CriarEntregaEpiCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ComandoPara(trabalhador, catalogo), default));

        Assert.Contains("NR-06", ex.Message);
        Assert.Empty(await db.EntregasEpi.ToListAsync());
    }

    [Fact]
    public async Task Handle_Nr6Vencida_Bloqueia()
    {
        var db = CriarDb(nameof(Handle_Nr6Vencida_Bloqueia));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, comNr6: false);
        var curso = await CriarCursoNr6Async(db);
        await AdicionarTreinamentoNr6Async(db, trabalhador.Id, curso, DateTime.UtcNow.AddYears(-3), DateTime.UtcNow.AddDays(-30));
        var handler = new CriarEntregaEpiCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ComandoPara(trabalhador, catalogo), default));

        Assert.Contains("vencido", ex.Message);
    }

    [Fact]
    public async Task Handle_CursoSemMarcadorAtendeNr6_Bloqueia()
    {
        var db = CriarDb(nameof(Handle_CursoSemMarcadorAtendeNr6_Bloqueia));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, comNr6: false);
        var outroCurso = new CursoTreinamento { Nome = "NR-35 Altura", NormaReferencia = "NR-35", AtendeNr6 = false };
        db.CursosTreinamento.Add(outroCurso);
        await db.SaveChangesAsync();
        await AdicionarTreinamentoNr6Async(db, trabalhador.Id, outroCurso, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddYears(1));
        var handler = new CriarEntregaEpiCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ComandoPara(trabalhador, catalogo), default));
    }

    [Fact]
    public async Task Handle_VenceHojeNoFusoDeBrasilia_AindaLibera()
    {
        var db = CriarDb(nameof(Handle_VenceHojeNoFusoDeBrasilia_AindaLibera));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, comNr6: false);
        var curso = await CriarCursoNr6Async(db);
        // Validade = hoje em Brasília (UtcNow - 3h): depois das 21h BRT o UtcNow.Date já é amanhã.
        await AdicionarTreinamentoNr6Async(db, trabalhador.Id, curso, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddHours(-3).Date);
        var handler = new CriarEntregaEpiCommandHandler(db);

        var id = await handler.Handle(ComandoPara(trabalhador, catalogo), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Handle_VariasNr6_UsaValidadeMaisLongaEIgnoraPayload()
    {
        var db = CriarDb(nameof(Handle_VariasNr6_UsaValidadeMaisLongaEIgnoraPayload));
        var (trabalhador, catalogo) = await CriarTrabalhadorECatalogoAsync(db, comNr6: false);
        var curso = await CriarCursoNr6Async(db);
        var realizacaoValida = DateTime.UtcNow.AddMonths(-2).Date;
        // Certificado antigo (vencido) lançado DEPOIS do novo não pode ofuscar o válido.
        await AdicionarTreinamentoNr6Async(db, trabalhador.Id, curso, realizacaoValida, DateTime.UtcNow.AddYears(1), "LP-VALIDA");
        await AdicionarTreinamentoNr6Async(db, trabalhador.Id, curso, DateTime.UtcNow.AddYears(-5), DateTime.UtcNow.AddYears(-3), "LP-ANTIGA");
        var handler = new CriarEntregaEpiCommandHandler(db);
        var comando = ComandoPara(trabalhador, catalogo) with
        {
            NumeroListaPresencaNr6 = "FORJADO",
            DataTreinamentoNr6 = new DateTime(2000, 1, 1),
        };

        var id = await handler.Handle(comando, default);

        var entrega = await db.EntregasEpi.FirstAsync(e => e.Id == id);
        Assert.Equal("LP-VALIDA", entrega.NumeroListaPresencaNr6);
        Assert.Equal(realizacaoValida, entrega.DataTreinamentoNr6);
    }
}
