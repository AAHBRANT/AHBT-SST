using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.LeituraIa;

namespace AAHBRANT.SST.Application.Tests.LeituraIa;

public class MontagemLeituraIaTests
{
    private static readonly DateTime Hoje = new(2026, 10, 10);

    // Matriz 5x5 da AAHBRANT: produto <= 1 trivial, <= 4 baixo, <= 12 moderado, <= 20 alto, 25 crítico.
    private static readonly Dictionary<(int, int), int> Matriz = Enumerable.Range(1, 5)
        .SelectMany(p => Enumerable.Range(1, 5).Select(s => (p, s)))
        .ToDictionary(x => x, x => (x.p * x.s) switch { 1 => 1, <= 4 => 2, <= 12 => 3, <= 20 => 4, _ => 5 });

    [Theory]
    [InlineData("ANUAL", 12)]
    [InlineData("Bienal", 24)]
    [InlineData("SEMESTRAL", 6)]
    [InlineData("a cada 6 meses", 6)]
    [InlineData("2 anos", 24)]
    [InlineData("", null)]
    public void Converte_periodicidade_em_meses(string texto, int? meses) => Assert.Equal(meses, MontagemLeituraIa.Meses(texto));

    [Fact]
    public void Periodicidade_casa_por_ordem_e_avisa_quando_a_contagem_nao_fecha()
    {
        // Caso real do PCMSO do Parque Roger: a coluna de periodicidade fica desalinhada no PDF.
        var leitura = new LeituraPcmsoIa(new CabecalhoPcmsoIa(null, null, null, null), new List<QuadroPcmsoIa>
        {
            new("PEDREIRO", "GHE 01", "715210",
                new List<ExameQuadroIa>
                {
                    new("ESPIROMETRIA [1057]", null, true, true, false, true, false),
                    new("HEMOGRAMA COMPLETO", "0693", true, true, false, true, false),
                    new("RAIO-X DE TORAX PADRAO OIT", "1415", true, true, false, true, false),
                },
                new List<string> { "BIENAL", "ANUAL", "BIENAL" }),
            new("PINTOR", "GHE 06", null,
                new List<ExameQuadroIa> { new("ECG", null, true, true, false, true, false), new("EEG", null, true, true, false, true, false) },
                new List<string> { "ANUAL" }),
        });

        var (exames, divergencias) = MontagemLeituraIa.MontarExamesPcmso(leitura);

        var pedreiro = exames.Single(e => e.Funcao == "PEDREIRO").Exames;
        Assert.Equal(new int?[] { 24, 12, 24 }, pedreiro.Select(e => e.PeriodicidadeMeses));
        Assert.Equal(("ESPIROMETRIA", "1057"), (pedreiro[0].Exame, pedreiro[0].Codigo));
        Assert.Contains(divergencias, d => d.Texto.Contains("PINTOR") && d.Texto.Contains("2 exames") && d.Texto.Contains("1 periodicidades"));
        Assert.Contains(divergencias, d => d.Texto.Contains("PINTOR") && d.Texto.Contains("EEG"));
    }

    [Fact]
    public void Confere_PxG_impresso_faixa_da_matriz_e_vigencia()
    {
        var leitura = new LeituraPgrIa(
            new CabecalhoPgrIa(null, null, null, null, null, new DateTime(2026, 10, 20)),
            new List<ImportarEstruturaGhe>
            {
                new(14, null, null, null, null, null, null,
                    new List<ImportarEstruturaFuncao> { new("Encanador", null, null, null, null) },
                    new List<ImportarEstruturaRisco>
                    {
                        new("Acidentes", "Animais peçonhentos", null, null, null, null, 3, 4, "8- Médio Risco", null),
                        new("Ergonômico", "Levantamento de peso", null, null, null, null, 2, 2, "4 - Médio Risco", null),
                        new("Físico", "Ruído", null, null, null, null, 1, 3, "3 - Baixo Risco", null),
                    }),
            },
            new List<string>());

        var d = MontagemLeituraIa.ConferirPgr(leitura, Matriz, Hoje);

        Assert.Contains(d, x => x.Gravidade == "alta" && x.Texto.Contains("PGR vence em 20/10/2026"));
        Assert.Contains(d, x => x.Texto.Contains("Animais peçonhentos") && x.Texto.Contains("= 12") && x.Texto.Contains("imprime 8"));
        Assert.Contains(d, x => x.Texto.Contains("Levantamento de peso") && x.Texto.Contains("Médio") && x.Texto.Contains("Baixo"));
        Assert.DoesNotContain(d, x => x.Texto.Contains("Ruído"));
    }

    [Fact]
    public void Casa_funcoes_pelo_nome_genero_e_partes()
    {
        var existentes = new List<MontagemLeituraIa.FuncaoExistente>
        {
            new(Guid.NewGuid(), "PEDREIRO"),
            new(Guid.NewGuid(), "Servente"),
            new(Guid.NewGuid(), "Engenheiro de Produção"),
            new(Guid.NewGuid(), "Auxiliar de Topografia"),
        };

        var casadas = MontagemLeituraIa.CasarFuncoes(new[]
        {
            ("Pedreiro", (string?)"715210", (int?)1, 0),
            ("Ajudante/Servente", null, 1, 0),
            ("Engenheira de Produção", null, 11, 0),
            ("Ajudante de Eletricista", null, 4, 0),
            ("Auxiliar de Topógrafo", null, 10, 0),
        }, existentes);

        Assert.Equal("existe", casadas.Single(f => f.NomeDocumento == "Pedreiro").Situacao);
        Assert.Equal(("parecida", "Servente"), casadas.Single(f => f.NomeDocumento == "Ajudante/Servente") is var a ? (a.Situacao, a.FuncaoNomeSugerida) : default);
        Assert.Equal("parecida", casadas.Single(f => f.NomeDocumento == "Engenheira de Produção").Situacao);
        Assert.Equal("nova", casadas.Single(f => f.NomeDocumento == "Ajudante de Eletricista").Situacao);
        Assert.Equal(("parecida", "Auxiliar de Topografia"), casadas.Single(f => f.NomeDocumento == "Auxiliar de Topógrafo") is var t ? (t.Situacao, t.FuncaoNomeSugerida) : default);
    }

    [Fact]
    public void Compara_nova_revisao_com_a_estrutura_atual()
    {
        var novos = new List<ImportarEstruturaGhe>
        {
            new(1, null, null, null, null, null, null,
                new List<ImportarEstruturaFuncao> { new("Pedreiro", null, null, null, null) },
                new List<ImportarEstruturaRisco> { new("Acidentes", "Trabalho em altura", null, null, null, null, 3, 4, null, null) }),
            new(16, null, null, null, null, null, null,
                new List<ImportarEstruturaFuncao> { new("Operador de Betoneira", null, null, null, null) },
                new List<ImportarEstruturaRisco>()),
        };
        var pedreiro = Guid.NewGuid();
        var funcoesAtuais = new Dictionary<int, List<MontagemLeituraIa.FuncaoAtual>>
        {
            [1] = new() { new(pedreiro, "PEDREIRO"), new(Guid.NewGuid(), "Servente") },
            [12] = new() { new(Guid.NewGuid(), "Vigia") },
        };
        var riscosAtuais = new List<MontagemLeituraIa.RiscoAtual> { new(1, "Trabalho em altura", 3) };
        // "Pedreiro" do documento ligado à função PEDREIRO do sistema: não é mudança, mesmo com outra grafia.
        var ligadas = new Dictionary<string, Guid?> { ["pedreiro"] = pedreiro, ["operador de betoneira"] = null };

        var dif = MontagemLeituraIa.CompararPgr(novos, funcoesAtuais, riscosAtuais, Matriz, ligadas);

        Assert.DoesNotContain(dif, d => d.Oque.Contains("função Pedreiro"));

        Assert.Contains(dif, d => d.Tipo == "incluido" && d.Oque.StartsWith("GHE 16"));
        Assert.Contains(dif, d => d.Tipo == "removido" && d.Oque.StartsWith("GHE 12"));
        Assert.Contains(dif, d => d.Tipo == "removido" && d.Oque == "GHE 01 · função Servente");
        // 3×4 = 12 continua Moderado: não é alteração.
        Assert.DoesNotContain(dif, d => d.Tipo == "alterado");
    }

    [Fact]
    public void Periodicidades_do_texto_seguem_a_ordem_da_coluna_do_quadro_da_funcao()
    {
        // Trecho real do PCMSO do Parque Roger (pág. 53, texto do PdfPig), com o quadro do Pintor depois.
        const string texto = """
            QUADRO DE EXAMES
            SETOR: CANTEIRO DE OBRAS FUNÇÃO: PEDREIRO CBO: 715210 GHE: GHE 01
            EXAMES RECOMENDADOS
            ADMISSIONAL DEMISSIONAL RETORNO AO TRABALHO MUDANÇA DE RISCOS OCUP. EXAME PERIODICIDADE
            ACUIDADE VISUAL [0296] ; ACUIDADE VISUAL [0296] ; ANUAL AUDIOMETRIA TONAL [0281]  EXAME CLINICO [0295] ;
            AUDIOMETRIA TONAL [0281] ; AUDIOMETRIA TONAL [0281] ; ANUAL ; AUDIOMETRIA TONAL [0281] ;
            AVALIACAO PSICOSSOCIAL [0300]  AVALIACAO PSICOSSOCIAL [0300]  ANUAL EXAME CLINICO [0295] ;
            ELETROCARDIOGRAMA [0530] ; ELETROCARDIOGRAMA [0530] ; ANUAL ELETROCARDIOGRAMA [0530] ;
            ELETROENCEFALOGRAMA [0536]  ELETROENCEFALOGRAMA [0536]  BIENAL ELETROENCEFALOGRAMA [0536]
            ; ; BIENAL ;
            ESPIROMETRIA [1057] ; ESPIROMETRIA [1057] ; ANUAL ESPIROMETRIA [1057] ;
            EXAME CLINICO [0295] ; EXAME CLINICO [0295] ; ANUAL EXAME CLINICO [0295] ;
            GLICEMIA EM JEJUM [0658] ; GLICEMIA EM JEJUM [0658] ; ANUAL GLICEMIA EM JEJUM [0658] ;
            HEMOGRAMA COMPLETO [0693] ; HEMOGRAMA COMPLETO [0693] ; BIENAL HEMOGRAMA COMPLETO [0693] ;
            RAIO-X DE TORAX PADRAO OIT  RAIO-X DE TORAX PADRAO OIT    RAIO-X DE TORAX PADRAO OIT
            QUADRO DE EXAMES
            SETOR: CANTEIRO DE OBRAS FUNÇÃO: PINTOR CBO: GHE: GHE 06
            EXAMES RECOMENDADOS
            ACIDO HIPURICO [1234] ; SEMESTRAL
            """;

        Assert.Equal(new[] { "ANUAL", "ANUAL", "ANUAL", "ANUAL", "BIENAL", "BIENAL", "ANUAL", "ANUAL", "ANUAL", "BIENAL" },
            MontagemLeituraIa.PeriodicidadesNoTexto(texto, "Pedreiro"));
        Assert.Equal(new[] { "SEMESTRAL" }, MontagemLeituraIa.PeriodicidadesNoTexto(texto, "PINTOR"));
        Assert.Empty(MontagemLeituraIa.PeriodicidadesNoTexto(texto, "Soldador"));
    }

    [Fact]
    public void Periodicidade_do_texto_tem_preferencia_quando_a_contagem_fecha()
    {
        var exames = Enumerable.Range(1, 3).Select(i => new ExameQuadroIa($"EXAME {i}", null, true, true, false, true, false)).ToList();
        var leitura = new LeituraPcmsoIa(new CabecalhoPcmsoIa(null, null, null, null), new List<QuadroPcmsoIa>
        {
            // A IA perdeu um valor; o texto tem os três.
            new("PEDREIRO", null, null, exames, new List<string> { "ANUAL", "BIENAL" }, new List<string> { "ANUAL", "BIENAL", "BIENAL" }),
        });

        var (resultado, divergencias) = MontagemLeituraIa.MontarExamesPcmso(leitura);

        Assert.Equal(new int?[] { 12, 24, 24 }, resultado.Single().Exames.Select(e => e.PeriodicidadeMeses));
        Assert.Empty(divergencias);
    }

    [Fact]
    public void Exame_que_a_IA_deixou_fora_do_periodico_volta_quando_o_texto_fecha_com_o_admissional()
    {
        // Caso real: a IA marcou o RX (código na linha de baixo do PDF) como fora do periódico.
        var exames = new List<ExameQuadroIa>
        {
            new("ESPIROMETRIA", "1057", true, true, false, true, false),
            new("HEMOGRAMA COMPLETO", "0693", true, true, false, true, false),
            new("RAIO-X DE TORAX PADRAO OIT", "1415", true, false, false, true, false),
        };
        var leitura = new LeituraPcmsoIa(new CabecalhoPcmsoIa(null, null, null, null), new List<QuadroPcmsoIa>
        {
            new("ARMADOR", null, null, exames, new List<string> { "BIENAL", "ANUAL" }, new List<string> { "BIENAL", "ANUAL", "BIENAL" }),
        });

        var (resultado, divergencias) = MontagemLeituraIa.MontarExamesPcmso(leitura);

        Assert.Equal(new int?[] { 24, 12, 24 }, resultado.Single().Exames.Select(e => e.PeriodicidadeMeses));
        Assert.Contains(divergencias, d => d.Texto.Contains("deduzidos dos do admissional"));
    }

    [Fact]
    public void Exames_ficam_na_ordem_do_quadro_do_PDF()
    {
        const string texto = """
            QUADRO DE EXAMES
            SETOR: CANTEIRO DE OBRAS FUNÇÃO: SOLDADOR CBO: GHE: GHE 05
            EXAMES RECOMENDADOS
            ELETROCARDIOGRAMA [0530] ; ELETROCARDIOGRAMA [0530] ; ANUAL
            ESPIROMETRIA [1057] ; ESPIROMETRIA [1057] ; BIENAL
            RAIO-X DE TORAX PADRAO OIT
            [1415] ; BIENAL
            """;
        var daIa = new List<ExameQuadroIa>
        {
            new("ESPIROMETRIA", "1057", true, true, false, true, false),
            new("RAIO-X DE TORAX PADRAO OIT", null, true, true, false, true, false),
            new("ELETROCARDIOGRAMA", "0530", true, true, false, true, false),
        };

        var ordenados = MontagemLeituraIa.OrdenarExamesPeloTexto(daIa, texto, "Soldador");

        Assert.Equal(new[] { "ELETROCARDIOGRAMA", "ESPIROMETRIA", "RAIO-X DE TORAX PADRAO OIT" }, ordenados.Select(e => e.Exame));
    }

    // Texto real do PCMSO do Parque Roger: a 1ª linha traz audiometria e exame clínico das colunas de
    // demissional/retorno — a ordem tem de vir da 1ª coluna de cada linha, não da 1ª ocorrência.
    [Fact]
    public void Ordem_do_quadro_ignora_exames_de_outras_colunas_na_mesma_linha()
    {
        const string texto = """
            QUADRO DE EXAMES
            SETOR: CANTEIRO DE OBRAS FUNÇÃO: PEDREIRO CBO: 715210 GHE: GHE 02
            EXAMES RECOMENDADOS
            ADMISSIONAL DEMISSIONAL RETORNO AO TRABALHO MUDANÇA DE RISCOS OCUP. EXAME PERIODICIDADE
            ACUIDADE VISUAL [0296] ; ACUIDADE VISUAL [0296] ; ANUAL AUDIOMETRIA TONAL [0281]  EXAME CLINICO [0295] ; ACUIDADE VISUAL [0296] ;
            AUDIOMETRIA TONAL [0281] ; AUDIOMETRIA TONAL [0281] ; ANUAL ; AUDIOMETRIA TONAL [0281] ;
            AVALIACAO PSICOSSOCIAL [0300]  AVALIACAO PSICOSSOCIAL [0300]  ANUAL EXAME CLINICO [0295] ; AVALIACAO PSICOSSOCIAL [0300]
            ; ;   ;
            ELETROCARDIOGRAMA [0530] ; ELETROCARDIOGRAMA [0530] ; ANUAL ELETROCARDIOGRAMA [0530] ;
            ELETROENCEFALOGRAMA [0536]  ELETROENCEFALOGRAMA [0536]  BIENAL ELETROENCEFALOGRAMA [0536]
            ; ; BIENAL ;
            ESPIROMETRIA [1057] ; ESPIROMETRIA [1057] ; ANUAL ESPIROMETRIA [1057] ;
            EXAME CLINICO [0295] ; EXAME CLINICO [0295] ; ANUAL EXAME CLINICO [0295] ;
            GLICEMIA EM JEJUM [0658] ; GLICEMIA EM JEJUM [0658] ; ANUAL GLICEMIA EM JEJUM [0658] ;
            HEMOGRAMA COMPLETO [0693] ; HEMOGRAMA COMPLETO [0693] ; BIENAL HEMOGRAMA COMPLETO [0693] ;
            RAIO-X DE TORAX PADRAO OIT  RAIO-X DE TORAX PADRAO OIT    RAIO-X DE TORAX PADRAO OIT
            [1415] ; [1415] ;  [1415] ;
            """;
        var esperado = new[]
        {
            "ACUIDADE VISUAL", "AUDIOMETRIA TONAL", "AVALIACAO PSICOSSOCIAL", "ELETROCARDIOGRAMA", "ELETROENCEFALOGRAMA",
            "ESPIROMETRIA", "EXAME CLINICO", "GLICEMIA EM JEJUM", "HEMOGRAMA COMPLETO", "RAIO-X DE TORAX PADRAO OIT",
        };
        var daIa = esperado.Reverse().Select(n => new ExameQuadroIa(n, null, true, true, false, true, false)).ToList();

        var ordenados = MontagemLeituraIa.OrdenarExamesPeloTexto(daIa, texto, "Pedreiro");

        Assert.Equal(esperado, ordenados.Select(e => e.Exame));
    }
}
