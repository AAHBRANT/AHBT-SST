using AAHBRANT.SST.Application.Catalogo;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Catalogo;

public class CatalogoAtividadesTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private class GeradorNumeroFake : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-TESTE-001");
    }

    private record Cenario(Guid ObraId, Guid AtividadeId);

    // Obra + atividade com 2 riscos: um Crítico (P5xS5) e um Baixo (P1xS2).
    private static async Task<Cenario> SemearAsync(IAppDbContext db, bool comRiscos = true)
    {
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        db.Pgrs.Add(new Pgr { ObraId = obra.Id, Nome = "PGR Obra Teste", DataElaboracao = DateTime.UtcNow });
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Trabalho em altura" };
        db.Atividades.Add(atividade);

        if (comRiscos)
        {
            var queda = new Perigo { Nome = "Queda de altura", Fonte = "Andaime" };
            var ruido = new Perigo { Nome = "Ruído", Fonte = "Furadeira" };
            db.Perigos.AddRange(queda, ruido);
            db.Riscos.Add(new Risco
            {
                AtividadeId = atividade.Id, PerigoId = queda.Id, Consequencia = "Óbito",
                Probabilidade = 5, Severidade = 5, NivelRisco = NivelRisco.Critico,
                ControlesExistentes = "Cinto tipo paraquedista", ControlesAdicionais = "Linha de vida",
            });
            db.Riscos.Add(new Risco
            {
                AtividadeId = atividade.Id, PerigoId = ruido.Id, Consequencia = "Perda auditiva",
                Probabilidade = 1, Severidade = 2, NivelRisco = NivelRisco.Baixo,
            });
        }

        await db.SaveChangesAsync();
        return new Cenario(obra.Id, atividade.Id);
    }

    [Fact]
    public async Task Catalogo_ListaAtividadeComRiscosEMaiorNivel()
    {
        var db = CriarDb(nameof(Catalogo_ListaAtividadeComRiscosEMaiorNivel));
        var c = await SemearAsync(db);

        var lista = await new ObterCatalogoAtividadesQueryHandler(db)
            .Handle(new ObterCatalogoAtividadesQuery(c.ObraId), default);

        var item = Assert.Single(lista);
        Assert.Equal("Trabalho em altura", item.Nome);
        Assert.Equal("Obra Teste", item.ObraNome);
        Assert.Equal("PGR Obra Teste", item.PgrNome);
        Assert.Equal(2, item.QuantidadeRiscos);
        Assert.Equal((int)NivelRisco.Critico, item.MaiorNivelRisco);
        Assert.Empty(item.Aprs);
        Assert.Empty(item.Pts);
    }

    [Fact]
    public async Task Catalogo_FiltraPorObra()
    {
        var db = CriarDb(nameof(Catalogo_FiltraPorObra));
        await SemearAsync(db);

        var lista = await new ObterCatalogoAtividadesQueryHandler(db)
            .Handle(new ObterCatalogoAtividadesQuery(Guid.NewGuid()), default);

        Assert.Empty(lista);
    }

    [Fact]
    public async Task Catalogo_AprAprovadaESemValidadeEVigente_EmElaboracaoNao()
    {
        var db = CriarDb(nameof(Catalogo_AprAprovadaESemValidadeEVigente_EmElaboracaoNao));
        var c = await SemearAsync(db);
        db.Aprs.Add(new Apr { AtividadeId = c.AtividadeId, Local = "A", Data = DateTime.UtcNow, Status = StatusApr.Aprovada });
        db.Aprs.Add(new Apr { AtividadeId = c.AtividadeId, Local = "B", Data = DateTime.UtcNow.AddDays(-1), Status = StatusApr.EmElaboracao });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ObterCatalogoAtividadesQueryHandler(db)
            .Handle(new ObterCatalogoAtividadesQuery(c.ObraId), default));

        Assert.Equal(2, item.Aprs.Count);
        Assert.Single(item.Aprs, a => a.Vigente);
        Assert.Contains(item.Aprs, a => !a.Vigente && a.Status == (int)StatusApr.EmElaboracao);
    }

    [Fact]
    public async Task Catalogo_PtAutorizadaComValidadeVencidaNaoEVigente()
    {
        var db = CriarDb(nameof(Catalogo_PtAutorizadaComValidadeVencidaNaoEVigente));
        var c = await SemearAsync(db);
        db.PermissoesTrabalho.Add(new PermissaoTrabalho
        {
            AtividadeId = c.AtividadeId, DescricaoAtividade = "x", Local = "A",
            Data = DateTime.UtcNow.AddDays(-10), Validade = DateTime.UtcNow.AddDays(-2), Status = StatusPt.Autorizada,
        });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ObterCatalogoAtividadesQueryHandler(db)
            .Handle(new ObterCatalogoAtividadesQuery(c.ObraId), default));

        Assert.False(Assert.Single(item.Pts).Vigente);
    }

    [Fact]
    public async Task GerarApr_CriaEmElaboracaoUmaEtapaPorRiscoOrdenadaPelaGravidade()
    {
        var db = CriarDb(nameof(GerarApr_CriaEmElaboracaoUmaEtapaPorRiscoOrdenadaPelaGravidade));
        var c = await SemearAsync(db);

        var id = await new GerarAprDaAtividadeCommandHandler(db, new GeradorNumeroFake())
            .Handle(new GerarAprDaAtividadeCommand(c.AtividadeId, "Frente 1", null, DateTime.UtcNow, null), default);

        var apr = await db.Aprs.Include(a => a.Etapas).ThenInclude(e => e.Riscos).SingleAsync(a => a.Id == id);
        Assert.Equal(StatusApr.EmElaboracao, apr.Status);
        Assert.Equal("APR-TESTE-001", apr.NumeroApr);
        Assert.Equal("PGR Obra Teste", apr.PgrReferencia);
        Assert.Equal(2, apr.Etapas.Count);

        var primeira = apr.Etapas.OrderBy(e => e.Ordem).First();
        Assert.Equal("Queda de altura", primeira.Descricao);
        var risco = Assert.Single(primeira.Riscos);
        Assert.Equal("Cinto tipo paraquedista | Linha de vida", risco.MedidasPrevencao);
        // Residual começa igual ao inicial (quem revisa reavalia).
        Assert.Equal(risco.NivelRiscoInicial, risco.NivelRiscoResidual);
    }

    [Fact]
    public async Task GerarApr_AtividadeSemRiscos_LancaInvalidOperation()
    {
        var db = CriarDb(nameof(GerarApr_AtividadeSemRiscos_LancaInvalidOperation));
        var c = await SemearAsync(db, comRiscos: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GerarAprDaAtividadeCommandHandler(db, new GeradorNumeroFake())
                .Handle(new GerarAprDaAtividadeCommand(c.AtividadeId, "Frente 1", null, DateTime.UtcNow, null), default));
    }

    [Fact]
    public async Task GerarApr_AtividadeInexistente_LancaKeyNotFound()
    {
        var db = CriarDb(nameof(GerarApr_AtividadeInexistente_LancaKeyNotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GerarAprDaAtividadeCommandHandler(db, new GeradorNumeroFake())
                .Handle(new GerarAprDaAtividadeCommand(Guid.NewGuid(), "Frente 1", null, DateTime.UtcNow, null), default));
    }

    [Fact]
    public async Task GerarPt_SoRiscosAltoOuCriticoViramRiscosCriticos_ENaoAutoriza()
    {
        var db = CriarDb(nameof(GerarPt_SoRiscosAltoOuCriticoViramRiscosCriticos_ENaoAutoriza));
        var c = await SemearAsync(db);

        var id = await new GerarPtDaAtividadeCommandHandler(db, new GeradorNumeroFake())
            .Handle(new GerarPtDaAtividadeCommand(c.AtividadeId, "Frente 1", null, DateTime.UtcNow, null), default);

        var pt = await db.PermissoesTrabalho.Include(p => p.RiscosCriticos).SingleAsync(p => p.Id == id);
        Assert.Equal(StatusPt.EmElaboracao, pt.Status);
        Assert.Equal("PT-TESTE-001", pt.NumeroPt);
        Assert.Equal("Queda de altura", Assert.Single(pt.RiscosCriticos).RiscoCondicao);
    }

    [Fact]
    public async Task Gerar_ComEquipe_CopiaOsMembrosComoResponsaveis()
    {
        var db = CriarDb(nameof(Gerar_ComEquipe_CopiaOsMembrosComoResponsaveis));
        var c = await SemearAsync(db);
        var equipe = new Equipe { Nome = "Equipe A" };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync();
        db.Trabalhadores.Add(new Trabalhador { Nome = "Fulano", EquipeId = equipe.Id });
        db.Trabalhadores.Add(new Trabalhador { Nome = "Ciclano", EquipeId = equipe.Id });
        await db.SaveChangesAsync();

        var id = await new GerarAprDaAtividadeCommandHandler(db, new GeradorNumeroFake())
            .Handle(new GerarAprDaAtividadeCommand(c.AtividadeId, "Frente 1", equipe.Id, DateTime.UtcNow, null), default);

        var apr = await db.Aprs.Include(a => a.Responsaveis).SingleAsync(a => a.Id == id);
        Assert.Equal(2, apr.Responsaveis.Count);
    }
}
