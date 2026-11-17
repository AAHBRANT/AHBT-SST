using System.Text.Json;
using System.Text.Json.Nodes;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

public class ClassificarRelatoPessoaExternaTests
{
    private static readonly CorrespondenciaNomesTrabalhadores.Candidato Joao = new(Guid.NewGuid(), "João Pedro da Silva");

    private static PessoaExternaCitadaIa Externa(string nome, string? vinculo = "Transeunte", string? trecho = null)
        => new(nome, vinculo, trecho);

    [Fact]
    public void Localizar_ExternaQueBateComUmFuncionario_ViraLocalizadaENaoExterna()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            Array.Empty<string>(), new[] { Joao }, new[] { Externa("João Pedro") });

        Assert.Equal(new[] { Joao.Id }, r.Localizados);
        Assert.Empty(r.Externas);
        Assert.Equal(new[] { Joao.Id }, r.LocalizadosDeExternas);
        var aviso = Assert.Single(r.Pendencias);
        Assert.Contains("pessoa externa", aviso);
        Assert.Contains("Confirme", aviso);
    }

    [Fact]
    public void Localizar_ExternaSoComPrimeiroNome_NaoViraFuncionario()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            Array.Empty<string>(), new[] { Joao }, new[] { Externa("seu João") });

        Assert.Empty(r.Localizados);
        Assert.Single(r.Externas);
        Assert.Empty(r.LocalizadosDeExternas);
        Assert.Empty(r.Pendencias);
    }

    [Fact]
    public void Localizar_ExternaAmbigua_FicaExternaComAviso()
    {
        var outro = new CorrespondenciaNomesTrabalhadores.Candidato(Guid.NewGuid(), "João Pedro Santos");
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            Array.Empty<string>(), new[] { Joao, outro }, new[] { Externa("João Pedro") });

        Assert.Empty(r.Localizados);
        Assert.Single(r.Externas);
        Assert.Contains("2 funcionários", Assert.Single(r.Pendencias));
    }

    [Fact]
    public void Localizar_NomeCitadoSubconjuntoDaExterna_NaoGeraAvisoDeNaoEncontrado()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            new[] { "Severino" }, new[] { Joao }, new[] { Externa("Severino Joaquim do Nascimento") });

        Assert.Empty(r.Pendencias);
        Assert.Single(r.Externas);
    }

    [Fact]
    public void Localizar_ExternaSemCorrespondencia_FicaExternaESemAviso()
    {
        // A IA também repetiu o nome em nomesCitados: não pode virar "não encontrado no cadastro".
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            new[] { "Severino Joaquim do Nascimento" }, new[] { Joao },
            new[] { Externa("Severino Joaquim do Nascimento", "Transeunte", "ciclista caiu na escavação") });

        Assert.Empty(r.Localizados);
        Assert.Single(r.Externas);
        Assert.Empty(r.Pendencias);
    }

    [Fact]
    public void Localizar_AvisoSoParaFuncionarioNaoEncontrado()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(
            new[] { "Carlos Mendes" }, new[] { Joao }, new[] { Externa("Severino Joaquim") });

        Assert.Single(r.Pendencias);
        Assert.Contains("Carlos Mendes", r.Pendencias[0]);
        Assert.Single(r.Externas);
    }

    [Theory]
    [InlineData("Terceiro", VinculoPessoaExterna.Terceiro)]
    [InlineData("  visitante ", VinculoPessoaExterna.Visitante)]
    [InlineData("TRANSEUNTE", VinculoPessoaExterna.Transeunte)]
    [InlineData("Trânseunte", VinculoPessoaExterna.Transeunte)]
    [InlineData("Morador", VinculoPessoaExterna.Outro)]
    [InlineData(null, VinculoPessoaExterna.Outro)]
    public void NormalizarVinculo_SemAcentoECaixa_DesconhecidoViraOutro(string? texto, VinculoPessoaExterna esperado)
        => Assert.Equal(esperado, ClassificarRelatoOcorrenciaCommandHandler.NormalizarVinculo(texto));

    private static async Task<RelatoOcorrenciaSugestaoDto> Executar(string tipo, string gravidade, bool? afastou, int? dias,
        IReadOnlyList<string> nomes, IReadOnlyList<PessoaExternaCitadaIa>? externas, params Trabalhador[] funcionarios)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Add(obra);
        foreach (var f in funcionarios) { f.ObraId = obra.Id; db.Add(f); }
        await db.SaveChangesAsync();
        var falso = new ClassificadorFixo(new OcorrenciaSugeridaIa(
            tipo, gravidade, null, null, null, "d", null, null, null, afastou, dias, null,
            nomes, Array.Empty<PerguntaRelatoOcorrencia>(), externas));
        return await new ClassificarRelatoOcorrenciaCommandHandler(db, falso)
            .Handle(new ClassificarRelatoOcorrenciaCommand("relato", obra.Id), default);
    }

    [Fact]
    public async Task Handle_PessoaExterna_DevolvidaSeparadaESemAfastamento()
    {
        var r = await Executar("OcorrenciaComPessoaExterna", "ComAfastamento", true, 5, Array.Empty<string>(),
            new[] { Externa("Severino Joaquim do Nascimento", "terceiro", "ciclista caiu") });

        Assert.Equal(TipoOcorrencia.OcorrenciaComPessoaExterna, r.Tipo);
        Assert.False(r.HouveAfastamento);
        Assert.Null(r.DiasAfastamento);
        Assert.Equal(GravidadeAcidente.SemAfastamento, r.Gravidade);
        var p = Assert.Single(r.PessoasExternasSugeridas);
        Assert.Equal(VinculoPessoaExterna.Terceiro, p.Vinculo);
        Assert.Equal("ciclista caiu", p.Trecho);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public async Task Handle_ExternaQueEraFuncionario_TipoExternoCaiParaAcidente()
    {
        var r = await Executar("OcorrenciaComPessoaExterna", "SemAfastamento", false, null, Array.Empty<string>(),
            new[] { Externa("João Pedro") }, new Trabalhador { Nome = "João Pedro da Silva" });

        Assert.Equal(TipoOcorrencia.Acidente, r.Tipo);
        Assert.Single(r.TrabalhadoresIds);
        Assert.Empty(r.PessoasExternasSugeridas);
    }

    [Fact]
    public async Task Handle_ExternaQueEraFuncionarioMaisOutraExterna_AcidenteSemForcarAfastamento()
    {
        var r = await Executar("OcorrenciaComPessoaExterna", "ComAfastamento", true, 4, Array.Empty<string>(),
            new[] { Externa("João Pedro"), Externa("Severino Joaquim") }, new Trabalhador { Nome = "João Pedro da Silva" });

        Assert.Equal(TipoOcorrencia.Acidente, r.Tipo);
        Assert.True(r.HouveAfastamento);
        Assert.Equal(4, r.DiasAfastamento);
        Assert.Equal(GravidadeAcidente.ComAfastamento, r.Gravidade);
        Assert.Equal("Severino Joaquim", Assert.Single(r.PessoasExternasSugeridas).Nome);
        Assert.Contains(r.Avisos, a => a.Contains("Confirme"));
    }

    [Fact]
    public async Task Handle_TestemunhaFuncionarioMaisExterna_MantemTipoExterno()
    {
        var r = await Executar("OcorrenciaComPessoaExterna", "SemAfastamento", false, null, new[] { "Carlos Mendes" },
            new[] { Externa("Severino Joaquim do Nascimento") }, new Trabalhador { Nome = "Carlos Mendes Souza" });

        Assert.Equal(TipoOcorrencia.OcorrenciaComPessoaExterna, r.Tipo);
        Assert.Single(r.TrabalhadoresIds);
        Assert.Single(r.PessoasExternasSugeridas);
    }

    [Fact]
    public async Task Handle_TipoExternoSemNenhumaPessoa_CaiParaIncidente()
    {
        var r = await Executar("OcorrenciaComPessoaExterna", "SemAfastamento", false, null, Array.Empty<string>(), null);

        Assert.Equal(TipoOcorrencia.Incidente, r.Tipo);
        Assert.Empty(r.PessoasExternasSugeridas);
    }

    [Fact]
    public async Task Handle_DuplicataComEspacosInternosMultiplos_Removida()
    {
        var r = await Executar("Acidente", "SemAfastamento", false, null, Array.Empty<string>(),
            new[] { Externa("Severino  Joaquim"), Externa("severino joaquim") });

        Assert.Equal("Severino Joaquim", Assert.Single(r.PessoasExternasSugeridas).Nome);
    }

    [Fact]
    public async Task Handle_LimitesDuplicatasEMaximoDePessoas()
    {
        var muitas = Enumerable.Range(1, 15).Select(i => Externa($"Pessoa Externa {i}")).ToList();
        muitas.Insert(0, Externa(new string('n', 250), "Visitante", new string('t', 400)));
        muitas.Insert(0, Externa("   ", "Visitante"));              // sem nome
        muitas.Insert(3, Externa("PESSOA EXTERNA 1"));              // duplicata (caixa)

        var r = await Executar("Acidente", "SemAfastamento", false, null, Array.Empty<string>(), muitas);

        Assert.Equal(10, r.PessoasExternasSugeridas.Count);
        Assert.Equal(200, r.PessoasExternasSugeridas[0].Nome.Length);
        Assert.Equal(300, r.PessoasExternasSugeridas[0].Trecho!.Length);
        Assert.Equal(r.PessoasExternasSugeridas.Count,
            r.PessoasExternasSugeridas.Select(p => p.Nome.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public async Task Handle_RespostaSemCampoNovo_ListaVazia()
    {
        var r = await Executar("Acidente", "SemAfastamento", false, null, Array.Empty<string>(), null);
        Assert.Empty(r.PessoasExternasSugeridas);
    }

    // Guarda contra o erro 400 do Claude: enum em campo cujo type inclui "null" ("Enum value ... does not match
    // declared type"). Vale para todo schema de saída estruturada dos fluxos de ocorrência.
    [Theory]
    [MemberData(nameof(Schemas))]
    public void Schema_NenhumNoMisturaEnumComNull(string nome, object schema)
    {
        var raiz = JsonSerializer.SerializeToNode(schema)!;
        var violacoes = new List<string>();
        Percorrer(raiz, "$", violacoes);
        Assert.True(violacoes.Count == 0, $"{nome}: enum junto com null em: {string.Join(", ", violacoes)}");
    }

    private static readonly string[] KeywordsNaoSuportadas =
        { "minItems", "maxItems", "minLength", "maxLength", "pattern", "format", "minimum", "maximum" };

    // Regras do modo strict do Claude: todo objeto fecha (additionalProperties=false), exige todas as chaves,
    // não usa keywords de validação e não passa de 16 uniões (type em array).
    [Theory]
    [MemberData(nameof(Schemas))]
    public void Schema_RespeitaRegrasDoModoStrict(string nome, object schema)
    {
        var raiz = JsonSerializer.SerializeToNode(schema)!;
        var problemas = new List<string>();
        var unioes = 0;
        Verificar(raiz, "$", problemas, ref unioes);
        Assert.True(unioes <= 16, $"{nome}: {unioes} uniões (limite 16)");
        Assert.True(problemas.Count == 0, $"{nome}: {string.Join("; ", problemas)}");
    }

    private static void Verificar(JsonNode? no, string caminho, List<string> problemas, ref int unioes)
    {
        if (no is JsonObject obj)
        {
            if (obj["type"] is JsonArray) unioes++;
            if (obj["properties"] is JsonObject props)
            {
                if (obj["additionalProperties"] is not JsonValue ap || ap.GetValue<bool>())
                    problemas.Add($"{caminho} sem additionalProperties=false");
                var obrigatorias = (obj["required"] as JsonArray)?.Select(x => x!.GetValue<string>()).ToHashSet() ?? new HashSet<string>();
                foreach (var chave in props.Select(p => p.Key).Where(k => !obrigatorias.Contains(k)))
                    problemas.Add($"{caminho}.{chave} fora de required");
                foreach (var (chave, filho) in props) Verificar(filho, $"{caminho}.{chave}", problemas, ref unioes);
            }
            else
            {
                // keywords só são checadas fora de "properties" (onde o nome seria um campo, ex.: "format")
                foreach (var k in KeywordsNaoSuportadas.Where(obj.ContainsKey)) problemas.Add($"{caminho} usa {k}");
                if (obj["items"] is not null) Verificar(obj["items"], caminho + ".items", problemas, ref unioes);
                return;
            }
            foreach (var k in KeywordsNaoSuportadas.Where(obj.ContainsKey)) problemas.Add($"{caminho} usa {k}");
            if (obj["items"] is not null) Verificar(obj["items"], caminho + ".items", problemas, ref unioes);
        }
    }

    public static IEnumerable<object[]> Schemas() => new[]
    {
        new object[] { "classificador", AzureOpenAiClassificadorOcorrencia.Schema },
        new object[] { "plano de ação", AzureOpenAiAnalistaPlanoOcorrencia.Schema },
    };

    private static void Percorrer(JsonNode? no, string caminho, List<string> violacoes)
    {
        if (no is JsonObject obj)
        {
            if (obj.ContainsKey("enum") && obj["type"] is JsonArray tipos
                && tipos.Any(t => t?.GetValue<string>() == "null"))
                violacoes.Add(caminho);
            if (obj["enum"] is JsonArray valores && valores.Any(v => v is null))
                violacoes.Add(caminho + " (enum com null)");
            foreach (var (k, v) in obj) Percorrer(v, $"{caminho}.{k}", violacoes);
        }
        else if (no is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++) Percorrer(arr[i], $"{caminho}[{i}]", violacoes);
        }
    }

    [Fact]
    public void SchemaDoClassificador_TemPessoasExternasObrigatoriasEVinculoSemEnum()
    {
        var raiz = JsonSerializer.SerializeToNode(AzureOpenAiClassificadorOcorrencia.Schema)!;
        Assert.Contains("pessoasExternas", raiz["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("OcorrenciaComPessoaExterna",
            raiz["properties"]!["tipo"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        var item = raiz["properties"]!["pessoasExternas"]!["items"]!;
        Assert.Null(item["properties"]!["vinculoProvavel"]!["enum"]);
        Assert.False(item["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(3, item["required"]!.AsArray().Count);
    }

    private sealed class ClassificadorFixo : IClassificadorRelatoOcorrencia
    {
        private readonly OcorrenciaSugeridaIa _r;
        public ClassificadorFixo(OcorrenciaSugeridaIa r) => _r = r;
        public Task<OcorrenciaSugeridaIa> ClassificarAsync(string relato, IReadOnlyList<RespostaPerguntaRelato> complementos,
            DateTime agoraLocal, IReadOnlyList<string> atividadesDaObra, CancellationToken ct) => Task.FromResult(_r);
    }
}
