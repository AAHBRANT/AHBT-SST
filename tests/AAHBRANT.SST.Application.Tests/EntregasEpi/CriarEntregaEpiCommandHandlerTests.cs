using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class CriarEntregaEpiCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Trabalhador trabalhador, CatalogoEpi catalogo)> CriarTrabalhadorECatalogoAsync(IAppDbContext db)
    {
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), FuncaoId = Guid.NewGuid(), Nome = "Carlos Eduardo", Cpf = "00000000000" };
        var catalogo = new CatalogoEpi { Nome = "Capacete de Segurança", VidaUtilEmMeses = 12 };
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
