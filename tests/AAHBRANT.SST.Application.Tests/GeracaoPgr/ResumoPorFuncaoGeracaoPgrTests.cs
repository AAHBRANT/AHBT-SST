using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Resumo por função do PGR (23/11/2026): riscos, EPIs e treinamentos de cada função, juntando os GHEs dela.
public class ResumoPorFuncaoGeracaoPgrTests
{
    private static readonly List<(int P, int S, NivelRisco Nivel)> Matriz = Enumerable.Range(1, 5)
        .SelectMany(p => Enumerable.Range(1, 5).Select(s => (p, s, p * s >= 15 ? NivelRisco.Alto : p * s >= 8 ? NivelRisco.Moderado : NivelRisco.Baixo))).ToList();

    private static readonly List<RegraTreinamentoResumo> Regras = new()
    {
        new("NR-06", "Uso de EPI", true, null, null),
        new("NR-35", "Trabalho em Altura", false, "andaime, telhado", "Queda de altura"),
        new("NR-35", "Trabalho em Altura", false, "fachada", null),
        new("NR-10", "Segurança em Eletricidade", false, "choque eletrico", "Choque elétrico"),
        new("NR-12", "Máquinas", false, "betoneira", null),
    };

    private static RiscoGerado Risco(string perigo, int p, int s, params MedidaGerada[] medidas)
    {
        var r = new RiscoGerado { Perigo = perigo, Tipo = "Acidente", Probabilidade = p, Severidade = s };
        r.Medidas.AddRange(medidas);
        return r;
    }

    private static MedidaGerada Epi(string texto) => new() { Categoria = CategoriasMedida.Epi, Texto = texto, Decisao = "Aceita" };
    private static MedidaGerada Epc(string texto) => new() { Categoria = CategoriasMedida.Epc, Texto = texto, Decisao = "Aceita" };

    private static EstruturaGeracaoPgr Estrutura() => new()
    {
        Ghes =
        {
            new GheGerado
            {
                Numero = 1, Nome = "Canteiro",
                Funcoes = { new FuncaoGerada { Nome = "Pedreiro", Cbo = "715210" }, new FuncaoGerada { Nome = "Ajudante Geral" } },
                Atividades =
                {
                    new AtividadeGerada
                    {
                        CodigoEap = "01.1", Nome = "Alvenaria com betoneira",
                        Riscos =
                        {
                            Risco("Queda de altura", 3, 4),
                            Risco("Ruído", 5, 3, Epi("Use protetor auricular tipo plug.")),
                        },
                    },
                },
            },
            new GheGerado
            {
                Numero = 2, Nome = "Estrutura",
                Funcoes = { new FuncaoGerada { Nome = "Pedreiro" } },
                Atividades =
                {
                    new AtividadeGerada
                    {
                        CodigoEap = "02.1", Nome = "Concretagem",
                        Riscos =
                        {
                            Risco("Queda de altura", 4, 5, Epc("Instale guarda-corpo."), Epi("Use cinto paraquedista com talabarte duplo.")),
                            new RiscoGerado { Perigo = "Choque elétrico", Tipo = "Acidente", Probabilidade = 2, Severidade = 5, Excluido = true },
                        },
                    },
                },
            },
            new GheGerado
            {
                Numero = 3, Nome = "Escritório",
                Funcoes = { new FuncaoGerada { Nome = "Almoxarife" } },
                Atividades = { new AtividadeGerada { CodigoEap = "03.1", Nome = "Controle de estoque", Riscos = { Risco("Postura", 3, 2, Epc("Cadeira ergonômica.")) } } },
            },
        },
    };

    private static ResumoFuncaoPgrDto Funcao(string nome) =>
        ResumoPorFuncaoGeracaoPgr.Montar(Estrutura(), Matriz, Regras).Single(f => f.Nome == nome);

    [Fact]
    public void Funcao_em_dois_ghes_junta_os_riscos_e_fica_com_o_pior_nivel()
    {
        var pedreiro = Funcao("Pedreiro");

        Assert.Equal(new[] { 1, 2 }, pedreiro.Ghes.Select(g => g.Numero));
        Assert.Equal("715210", pedreiro.Cbo);
        var queda = pedreiro.Riscos.Single(r => r.Perigo == "Queda de altura");
        Assert.Equal((4, 5, NivelRisco.Alto), (queda.Probabilidade, queda.Severidade, queda.Nivel));
        Assert.Equal(2, queda.Medidas);
        Assert.True(pedreiro.TemQuedaDeAltura);
    }

    [Fact]
    public void Risco_sem_medida_aponta_o_ghe()
    {
        var pedreiro = Funcao("Pedreiro");

        Assert.Equal(new[] { 1 }, pedreiro.Riscos.Single(r => r.Perigo == "Queda de altura").GhesSemMedida);
        Assert.Equal(1, pedreiro.RiscosSemMedida);
        Assert.Equal(0, Funcao("Almoxarife").RiscosSemMedida);
    }

    [Fact]
    public void Risco_excluido_nao_entra()
    {
        Assert.DoesNotContain(Funcao("Pedreiro").Riscos, r => r.Perigo == "Choque elétrico");
        Assert.DoesNotContain(Funcao("Pedreiro").Treinamentos, t => t.Norma == "NR-10");
    }

    [Fact]
    public void Riscos_vem_do_mais_alto_para_o_mais_baixo()
    {
        Assert.Equal(new[] { "Queda de altura", "Ruído" }, Funcao("Pedreiro").Riscos.Select(r => r.Perigo));
    }

    [Fact]
    public void Epis_saem_das_medidas_de_epi_com_o_perigo_que_pede()
    {
        var epis = Funcao("Pedreiro").Epis;

        Assert.Contains(epis, e => e.Perigos.SequenceEqual(new[] { "Ruído" }));
        Assert.Contains(epis, e => e.Perigos.SequenceEqual(new[] { "Queda de altura" }));
        Assert.Empty(Funcao("Almoxarife").Epis);
    }

    [Fact]
    public void Treinamentos_pelas_regras_sem_repetir_a_mesma_nr()
    {
        var pedreiro = Funcao("Pedreiro");

        Assert.Equal(new[] { "NR-06", "NR-12", "NR-35" }, pedreiro.Treinamentos.Select(t => t.Norma));
        Assert.Equal(new[] { ResumoPorFuncaoGeracaoPgr.MotivoTodas }, pedreiro.Treinamentos[0].Motivos);
        Assert.Contains("Queda de altura", pedreiro.Treinamentos.Single(t => t.Norma == "NR-35").Motivos);
        Assert.Contains("Atividade 01.1 Alvenaria com betoneira", pedreiro.Treinamentos.Single(t => t.Norma == "NR-12").Motivos);
        Assert.Equal(new[] { "NR-06" }, Funcao("Almoxarife").Treinamentos.Select(t => t.Norma));
    }

    [Fact]
    public void Uma_linha_por_funcao_em_ordem_alfabetica()
    {
        var nomes = ResumoPorFuncaoGeracaoPgr.Montar(Estrutura(), Matriz, Regras).Select(f => f.Nome);

        Assert.Equal(new[] { "Ajudante Geral", "Almoxarife", "Pedreiro" }, nomes);
    }
}
