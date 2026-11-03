using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Ações de melhoria do plano de ação (comentário do engenheiro sobre o PGR do Beira Rio, 03/11/2026).
public class SugerirAcoesMelhoriaPlanoTests
{
    // P×S >= 15 é Alto; o resto, Baixo.
    private static readonly List<(int P, int S, NivelRisco Nivel)> Matriz = Enumerable.Range(1, 5)
        .SelectMany(p => Enumerable.Range(1, 5).Select(s => (p, s, p * s >= 15 ? NivelRisco.Alto : NivelRisco.Baixo))).ToList();

    private static EstruturaGeracaoPgr Estrutura(params RiscoGerado[] riscos) => new()
    {
        Ghes = { new GheGerado { Numero = 1, Nome = "Obra", Atividades = { new AtividadeGerada { CodigoEap = "1", Nome = "Vala", Riscos = riscos.ToList() } } } },
    };

    private static RiscoGerado Risco(string perigo, int p, int s, params (string Cat, string Texto)[] medidas) => new()
    {
        Perigo = perigo, Tipo = "Acidente", Probabilidade = p, Severidade = s,
        Medidas = medidas.Select(m => new MedidaGerada { Categoria = m.Cat, Texto = m.Texto }).ToList(),
    };

    [Fact]
    public void So_risco_alto_vira_acao_e_epi_fica_de_fora()
    {
        var e = Estrutura(
            Risco("Queda de altura", 4, 5, (CategoriasMedida.Epc, "Instalar guarda-corpo"), (CategoriasMedida.Epi, "Cinto paraquedista")),
            Risco("Ruído", 1, 2, (CategoriasMedida.Epc, "Enclausurar o equipamento")));

        var acoes = SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz);

        var a = Assert.Single(acoes);
        Assert.Equal("Instalar guarda-corpo (risco: Queda de altura)", a.Descricao);
        Assert.Null(a.ResponsavelUsuarioId);
        Assert.Null(a.Prazo);
    }

    [Fact]
    public void Nao_duplica_acao_ja_existente_nem_medida_repetida_em_outra_atividade()
    {
        var e = Estrutura(
            Risco("Soterramento", 3, 5, (CategoriasMedida.Eliminacao, "Escorar a vala")),
            Risco("Soterramento", 3, 5, (CategoriasMedida.Eliminacao, "Escorar a vala")));
        e.AcoesPlano.Add(new AcaoPlanoGerada { Descricao = "escorar a vala (risco: soterramento)" });

        Assert.Empty(SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz));
    }

    [Fact]
    public void Medida_sem_categoria_de_rascunho_antigo_entra_depois_das_classificadas()
    {
        var e = Estrutura(Risco("Queda de altura", 4, 5, (null!, "Sinalizar a área"), (CategoriasMedida.Epc, "Instalar guarda-corpo")));

        var acoes = SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz);

        Assert.Equal(new[] { "Instalar guarda-corpo (risco: Queda de altura)", "Sinalizar a área (risco: Queda de altura)" }, acoes.Select(a => a.Descricao));
    }

    [Fact]
    public void No_maximo_duas_acoes_por_perigo_com_as_de_maior_prioridade()
    {
        var e = Estrutura(Risco("Queda de altura", 4, 5,
            (CategoriasMedida.Administrativa, "Capacitar a equipe"), (CategoriasMedida.Epc, "Instalar guarda-corpo"),
            (CategoriasMedida.Eliminacao, "Eliminar o trabalho em altura"), (CategoriasMedida.Administrativa, "Inspecionar andaimes")));

        var acoes = SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(e, Matriz);

        Assert.Equal(new[] { "Eliminar o trabalho em altura (risco: Queda de altura)", "Instalar guarda-corpo (risco: Queda de altura)" }, acoes.Select(a => a.Descricao));
    }

    [Fact]
    public void Plano_recebe_no_maximo_cinco_acoes_comecando_pelas_de_maior_prioridade()
    {
        var riscos = Enumerable.Range(1, 6).Select(i => Risco($"Perigo {i}", 4, 5,
            (CategoriasMedida.Administrativa, $"Treinar {i}"), (CategoriasMedida.Epc, $"Proteger {i}"))).ToArray();

        var acoes = SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(Estrutura(riscos), Matriz);

        Assert.Equal(5, acoes.Count);
        Assert.All(acoes, a => Assert.StartsWith("Proteger", a.Descricao));
    }

    [Fact]
    public void Medida_descartada_e_risco_excluido_nao_entram()
    {
        var descartada = Risco("Queda", 5, 5, (CategoriasMedida.Epc, "Rede de proteção"));
        descartada.Medidas[0].Decisao = "Descartada";
        var excluido = Risco("Incêndio", 5, 5, (CategoriasMedida.Epc, "Extintores extras"));
        excluido.Excluido = true;

        Assert.Empty(SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.Montar(Estrutura(descartada, excluido), Matriz));
    }
}
