using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Remoção de um GHE do rascunho do PGR e das ações do plano que só existiam por causa dele (03/11/2026).
public class RemoverGheGeracaoPgrTests
{
    private static readonly List<(int P, int S, NivelRisco Nivel)> Matriz = Enumerable.Range(1, 5)
        .SelectMany(p => Enumerable.Range(1, 5).Select(s => (p, s, p * s >= 15 ? NivelRisco.Alto : NivelRisco.Baixo))).ToList();

    private static GheGerado Ghe(int numero, string codigo, string perigo, string medida) => new()
    {
        Numero = numero, Nome = $"GHE {numero}",
        Funcoes = { new FuncaoGerada { Nome = $"Funcao {numero}" } },
        Atividades =
        {
            new AtividadeGerada
            {
                CodigoEap = codigo, Nome = $"Atividade {codigo}",
                Riscos = { new RiscoGerado { Perigo = perigo, Tipo = "Acidente", Probabilidade = 4, Severidade = 5,
                    Medidas = { new MedidaGerada { Categoria = CategoriasMedida.Epc, Texto = medida } } } },
            },
        },
    };

    // Dois GHEs; as ações do plano saem das medidas, como no botão "Sugerir ações de melhoria".
    private static EstruturaGeracaoPgr Estrutura()
    {
        var e = new EstruturaGeracaoPgr { Ghes = { Ghe(1, "1", "Queda", "Guarda-corpo"), Ghe(2, "2", "Soterramento", "Escorar vala") } };
        e.AcoesPlano.AddRange(SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz));
        return e;
    }

    [Fact]
    public void Descartando_as_atividades_saem_funcoes_riscos_e_a_acao_gerada_do_ghe()
    {
        var e = Estrutura();
        Assert.Equal(2, e.AcoesPlano.Count);

        var r = RemoverGheGeracaoPgrCommandHandler.Remover(e, 2, null, Matriz);

        Assert.Equal(new RemoverGheResultado(1, 1, 1), r);
        Assert.Equal(new[] { 1 }, e.Ghes.Select(g => g.Numero));
        Assert.Equal(new[] { "Guarda-corpo (risco: Queda)" }, e.AcoesPlano.Select(a => a.Descricao));
    }

    [Fact]
    public void Movendo_as_atividades_os_riscos_e_as_acoes_continuam()
    {
        var e = Estrutura();

        var r = RemoverGheGeracaoPgrCommandHandler.Remover(e, 2, 1, Matriz);

        Assert.Equal(new RemoverGheResultado(1, 0, 0), r);
        Assert.Equal(new[] { "1", "2" }, e.Ghes.Single().Atividades.Select(a => a.CodigoEap));
        Assert.Equal(2, e.AcoesPlano.Count);
    }

    [Fact]
    public void Acao_editada_com_responsavel_com_prazo_ou_escrita_a_mao_nao_sai()
    {
        var e = Estrutura();
        var acaoGhe2 = e.AcoesPlano.Single(a => a.Descricao.StartsWith("Escorar"));
        acaoGhe2.ResponsavelNome = "Maria";
        e.AcoesPlano.Add(new AcaoPlanoGerada { Descricao = "Treinar a equipe" });
        e.AcoesPlano.Add(new AcaoPlanoGerada { Descricao = "Escorar vala melhor (risco: Soterramento)", DescricaoGerada = "Escorar vala (risco: Soterramento)" });
        e.AcoesPlano.Add(new AcaoPlanoGerada { Descricao = "Outra", DescricaoGerada = "Outra", Prazo = new DateTime(2026, 12, 1) });

        var r = RemoverGheGeracaoPgrCommandHandler.Remover(e, 2, null, Matriz);

        Assert.Equal(0, r.AcoesRemovidas);
        Assert.Equal(5, e.AcoesPlano.Count);
    }

    [Fact]
    public void Medida_que_outro_ghe_ainda_usa_continua_no_plano()
    {
        var e = new EstruturaGeracaoPgr { Ghes = { Ghe(1, "1", "Queda", "Guarda-corpo"), Ghe(2, "2", "Queda", "Guarda-corpo") } };
        e.AcoesPlano.AddRange(SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz));

        var r = RemoverGheGeracaoPgrCommandHandler.Remover(e, 2, null, Matriz);

        Assert.Equal(0, r.AcoesRemovidas);
        Assert.Single(e.AcoesPlano);
    }

    [Fact]
    public void Sem_matriz_o_plano_nao_e_tocado()
    {
        var e = Estrutura();

        var r = RemoverGheGeracaoPgrCommandHandler.Remover(e, 2, null, null);

        Assert.Equal(0, r.AcoesRemovidas);
        Assert.Equal(2, e.AcoesPlano.Count);
    }

    [Fact]
    public void Ghe_inexistente_ou_destino_invalido_e_recusado()
    {
        Assert.Throws<KeyNotFoundException>(() => RemoverGheGeracaoPgrCommandHandler.Remover(Estrutura(), 9, null, Matriz));
        Assert.Throws<KeyNotFoundException>(() => RemoverGheGeracaoPgrCommandHandler.Remover(Estrutura(), 1, 9, Matriz));
        Assert.Throws<InvalidOperationException>(() => RemoverGheGeracaoPgrCommandHandler.Remover(Estrutura(), 1, 1, Matriz));
    }
}
