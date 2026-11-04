using AAHBRANT.SST.Application.Funcoes;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Catalogo;

// Item e matriz de EPI por obra (04/11/2026), criados ao aprovar o PGR da obra.
public class MatrizEpiPorObraTests
{
    private static CatalogoEpi Item(string nome, Guid? obraId) => new() { Nome = nome, VidaUtilEmMeses = 12, ObraId = obraId };

    private static EstruturaGeracaoPgr PgrComMedidasDeEpi(string funcao, params string[] medidas) => new()
    {
        Ghes = new()
        {
            new GheGerado
            {
                Numero = 1, Nome = "Estrutura",
                Funcoes = new() { new FuncaoGerada { Nome = funcao, Descricao = "x" } },
                Atividades = new()
                {
                    new AtividadeGerada
                    {
                        CodigoEap = "1.1", Nome = "Alvenaria", RiscosGerados = true,
                        Riscos = new()
                        {
                            new RiscoGerado
                            {
                                Perigo = "Queda de materiais", Tipo = "Acidente",
                                Medidas = medidas.Select(m => new MedidaGerada { Texto = m, Categoria = CategoriasMedida.Epi }).ToList(),
                            },
                        },
                    },
                },
            },
        },
    };

    [Fact]
    public async Task Matriz_antiga_vale_enquanto_a_obra_nao_tem_a_propria()
    {
        var db = DbContextFactory.Criar();
        var (obraA, obraB) = (new Obra { Codigo = "A", Nome = "A" }, new Obra { Codigo = "B", Nome = "B" });
        var funcao = new Funcao { Nome = "Pedreiro" };
        var (antigo, deA) = (Item("Capacete antigo", null), Item("Capacete da A", obraA.Id));
        db.AddRange(obraA, obraB, funcao, antigo, deA);
        db.MatrizEpiFuncoes.AddRange(
            new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = antigo.Id },
            new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = deA.Id, ObraId = obraA.Id });
        await db.SaveChangesAsync();

        var daA = await db.MatrizEpiFuncoes.ParaObra(obraA.Id).Select(m => m.CatalogoEpiId).ToListAsync();
        var daB = await db.MatrizEpiFuncoes.ParaObra(obraB.Id).Select(m => m.CatalogoEpiId).ToListAsync();

        Assert.Equal(new[] { deA.Id }, daA);       // a obra A usa só a dela
        Assert.Equal(new[] { antigo.Id }, daB);    // a B segue na antiga e nunca vê a da A
    }

    [Fact]
    public async Task Aprovar_liga_o_tipo_de_epi_ao_item_da_obra_e_prefere_o_da_obra()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "A", Nome = "A" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var (daObra, antigo) = (Item("Capacete de segurança classe B", obra.Id), Item("Capacete genérico", null));
        db.AddRange(obra, funcao, daObra, antigo);
        await db.SaveChangesAsync();

        var acoes = await MatrizEpiDoPgr.AplicarAsync(db, PgrComMedidasDeEpi("Pedreiro", "Usar capacete em todo o canteiro."),
            obra.Id, new Dictionary<string, Funcao> { ["pedreiro"] = funcao }, default);
        await db.SaveChangesAsync();

        var matriz = await db.MatrizEpiFuncoes.IgnoreQueryFilters().ToListAsync();
        var ligacao = Assert.Single(matriz);
        Assert.Equal(daObra.Id, ligacao.CatalogoEpiId);
        Assert.Equal(obra.Id, ligacao.ObraId);
        Assert.Empty(acoes);
    }

    [Fact]
    public async Task Tipo_sem_item_no_catalogo_vira_acao_e_nada_e_criado_no_catalogo()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "A", Nome = "A" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.AddRange(obra, funcao, Item("Capacete de segurança", obra.Id));
        await db.SaveChangesAsync();

        var acoes = await MatrizEpiDoPgr.AplicarAsync(db, PgrComMedidasDeEpi("Pedreiro", "Usar capacete.", "Usar óculos de proteção."),
            obra.Id, new Dictionary<string, Funcao> { ["pedreiro"] = funcao }, default);
        await db.SaveChangesAsync();

        Assert.Single(await db.MatrizEpiFuncoes.IgnoreQueryFilters().ToListAsync());
        var acao = Assert.Single(acoes);
        Assert.Contains("Óculos de segurança", acao);
        Assert.Contains("Pedreiro", acao);
        Assert.Equal(1, await db.CatalogoEpis.CountAsync());
    }

    [Fact]
    public async Task Aprovar_de_novo_nao_duplica_e_reativa_a_ligacao_desativada()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "A", Nome = "A" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var capacete = Item("Capacete de segurança", obra.Id);
        db.AddRange(obra, funcao, capacete);
        await db.SaveChangesAsync();
        var estrutura = PgrComMedidasDeEpi("Pedreiro", "Usar capacete.");
        var funcoes = new Dictionary<string, Funcao> { ["pedreiro"] = funcao };

        await MatrizEpiDoPgr.AplicarAsync(db, estrutura, obra.Id, funcoes, default);
        await db.SaveChangesAsync();
        (await db.MatrizEpiFuncoes.SingleAsync()).Ativo = false;
        await db.SaveChangesAsync();
        await MatrizEpiDoPgr.AplicarAsync(db, estrutura, obra.Id, funcoes, default);
        await db.SaveChangesAsync();

        var todas = await db.MatrizEpiFuncoes.IgnoreQueryFilters().ToListAsync();
        var unica = Assert.Single(todas);
        Assert.True(unica.Ativo);
    }
}
