using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Estrutura de trabalho do assistente "Gerar PGR pela EAP" (21/10/2026), guardada como JSON em
// GeracaoPgrEap.EstruturaJson. A tela edita e devolve o objeto inteiro a cada passo; só a aprovação do
// Engenheiro de Segurança transforma isto em Pgr/Ghe/Atividade/Risco/MedidaPrevencao.
public class EstruturaGeracaoPgr
{
    public List<ItemEapGerado> Itens { get; set; } = new();
    public List<GheGerado> Ghes { get; set; } = new();
    // Fim da vigência do programa (23/10/2026): vira Pgr.DataTermino na aprovação e liga o alerta de PGR vencido.
    public DateTime? DataTermino { get; set; }
    public List<AcaoPlanoGerada> AcoesPlano { get; set; } = new();
    // Dados da obra que o PGR exige e o sistema não tem em outro lugar (24/10/2026): emergência, CNAE, CREA...
    public DadosComplementaresPgr Complementares { get; set; } = new();
    // Formato antigo (uma ação por linha, sem responsável nem prazo). Só existe para ler rascunhos feitos
    // antes de 23/10/2026: Normalizar() passa as linhas para AcoesPlano.
    public List<string> PlanoAcao { get; set; } = new();

    public EstruturaGeracaoPgr Normalizar()
    {
        foreach (var texto in PlanoAcao.Where(t => !string.IsNullOrWhiteSpace(t)))
            AcoesPlano.Add(new AcaoPlanoGerada { Descricao = texto.Trim() });
        PlanoAcao = new();
        Complementares ??= new();
        Complementares.SemearPadroes();
        return this;
    }
}

// Dados complementares do PGR: o técnico preenche uma vez e a tela já abre preenchida nas próximas gerações
// da mesma obra. Obrigatórios para aprovar, como o término da vigência.
// Uma das duas assinaturas do PGR: o técnico de segurança da obra (assina ao enviar para aprovação) ou o engenheiro (assina
// ao aprovar). AMao = assina fora do sistema, e a linha sai em branco no PDF. O registro profissional é digitado em cada PGR.
public class AssinanteGeracaoPgr
{
    public Guid? UsuarioId { get; set; }
    public string? Nome { get; set; }
    public string? Registro { get; set; }
    public bool AMao { get; set; }
}

public class DadosComplementaresPgr
{
    // Número e descrição, como no documento (ex.: "4211-1/01 — Construção de rodovias e ferrovias").
    public string? Cnae { get; set; }
    // Grau de risco da atividade, de 1 a 4 (NR-04).
    public int? GrauRisco { get; set; }
    public string? Cep { get; set; }
    // Registro profissional (CREA) de quem assina como responsável técnico.
    public string? ResponsavelTecnicoRegistro { get; set; }
    public string? ResponsavelImplantacaoNome { get; set; }
    public string? ResponsavelImplantacaoCargo { get; set; }
    public List<ContatoEmergencia> ContatosEmergencia { get; set; } = new();
    public List<string> CenariosEmergencia { get; set; } = new();
    // Quem assina o PGR (escolhido no passo 4). Nulos em rascunhos antigos, que seguem com a linha em branco no PDF.
    public AssinanteGeracaoPgr? Tecnico { get; set; }
    public AssinanteGeracaoPgr? Engenheiro { get; set; }
    // Quando e de onde as rotas foram calculadas (sugestão por mapa): vai no rodapé do plano de emergência.
    public DateTime? RotasCalculadasEmUtc { get; set; }
    public string? RotasFonte { get; set; }

    // Linhas e cenários iniciais, para a tela nunca abrir vazia. Só entram quando a lista está vazia.
    public void SemearPadroes()
    {
        if (ContatosEmergencia.Count == 0)
            ContatosEmergencia = new()
            {
                new() { Chave = "hospital", Local = "Hospital" },
                new() { Chave = "samu", Local = "SAMU", Telefone = "192" },
                new() { Chave = "bombeiros", Local = "Corpo de Bombeiros", Telefone = "193" },
                new() { Chave = "pm", Local = "Polícia Militar", Telefone = "190" },
                new() { Chave = "seguranca", Local = "Segurança do trabalho da obra" },
            };
        if (CenariosEmergencia.Count == 0)
            CenariosEmergencia = CenariosEmergenciaPgr.Padrao.ToList();
    }
}

public class ContatoEmergencia
{
    // "hospital", "samu", "bombeiros", "pm" ou "seguranca" nas linhas iniciais; nulo nas linhas que o técnico acrescenta.
    public string? Chave { get; set; }
    public string Local { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public string? Telefone { get; set; }
    // Rota até o local, calculada a partir da obra (sugestão por mapa). O técnico confere antes de aprovar.
    public double? DistanciaKm { get; set; }
    public int? TempoMinutos { get; set; }
    // Caminho em linguagem simples, um passo por linha.
    public string? Rota { get; set; }
    // Local que a IA recomenda entre as opções próximas (ex.: hospital mais adequado aos riscos da obra).
    public bool Recomendado { get; set; }
}

public static class CenariosEmergenciaPgr
{
    public static readonly string[] Todos =
    {
        "Acidente pessoal ou mal súbito",
        "Incêndio e explosão",
        "Vazamento ou derrame de óleos, graxas e líquidos combustíveis ou inflamáveis",
        "Acidente com eletricidade",
        "Acidente de trajeto",
        "Resgate em trabalho em altura",
        "Resgate em espaço confinado",
        "Soterramento ou deslizamento de talude",
        "Atropelamento ou acidente com máquinas",
    };

    // Os cinco cenários mínimos do modelo da obra; os demais o técnico marca quando se aplicam.
    public static readonly string[] Padrao = Todos.Take(5).ToArray();
}

// Categorias de medida de prevenção na ordem de prioridade da NR-01.
public static class CategoriasMedida
{
    public const string Eliminacao = "Eliminação";
    public const string Epc = "EPC";
    public const string Administrativa = "Administrativa";
    public const string Epi = "EPI";

    public static readonly string[] Todas = { Eliminacao, Epc, Administrativa, Epi };

    public static bool Valida(string? categoria) => categoria is not null && Todas.Contains(categoria);

    // Aceita a forma sem acento que a IA devolve no JSON; qualquer outra coisa fica sem classificação.
    public static string? Normalizar(string? categoria) => categoria?.Trim().ToLowerInvariant() switch
    {
        "eliminacao" or "eliminação" => Eliminacao,
        "epc" => Epc,
        "administrativa" => Administrativa,
        "epi" => Epi,
        _ => null,
    };
}

// Ação do plano de ação: o que fazer, quem é o responsável e até quando.
public class AcaoPlanoGerada
{
    public string Descricao { get; set; } = string.Empty;
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelNome { get; set; }
    // Início opcional: com ele o cronograma mensal marca todos os meses de Início a Prazo.
    public DateTime? Inicio { get; set; }
    public DateTime? Prazo { get; set; }

    // Acompanhamento e aferição de resultados (NR-01, item 1.5.5.2, apontamento da supervisão de 03/08/2026): como a
    // ação será acompanhada, com que frequência e o que mostra que funcionou. Opcionais; a IA sugere e o engenheiro revisa.
    public string? FormaAcompanhamento { get; set; }
    public string? PeriodicidadeAcompanhamento { get; set; }
    public string? CriterioAfericao { get; set; }
    // Verdadeiro enquanto o acompanhamento veio da IA e ninguém o conferiu; quem edita um campo da ação o desmarca.
    public bool AcompanhamentoSugeridoPelaIa { get; set; }
}

// Valores aceitos para forma e periodicidade do acompanhamento. Fora da lista a forma vira "Outro" e a periodicidade fica
// em branco, para o engenheiro escolher.
public static class AcompanhamentoPlano
{
    public static readonly string[] Formas =
    {
        "Inspeção periódica", "Registro de treinamento", "Indicador", "Auditoria", "Medição", "Outro",
    };

    public static readonly string[] Periodicidades =
    {
        "Semanal", "Quinzenal", "Mensal", "Trimestral", "A cada ocorrência",
    };

    public static string? Forma(string? valor) => Escolher(valor, Formas) ?? (string.IsNullOrWhiteSpace(valor) ? null : "Outro");

    public static string? Periodicidade(string? valor) => Escolher(valor, Periodicidades);

    private static string? Escolher(string? valor, string[] lista) =>
        lista.FirstOrDefault(x => string.Equals(x, valor?.Trim(), StringComparison.CurrentCultureIgnoreCase));
}

// Item da EAP lido do arquivo. Nivel 1 = pacote; 2+ = atividade. Usar = entra no PGR (frente de
// trabalho com exposição); Conferir = a IA não leu com segurança (nome ilegível, item duvidoso).
public class ItemEapGerado
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int Nivel { get; set; } = 1;
    public string? Periodo { get; set; }
    public bool Usar { get; set; } = true;
    public bool Conferir { get; set; }
    // Incluído à mão no passo 2 (29/10/2026): não veio do arquivo da EAP.
    public bool Manual { get; set; }
}

// Toda obra tem administrativo, e a EAP não traz (29/10/2026): o item entra sempre no passo 2, já marcado, e vira o
// GHE Administrativo. Quem não tiver essa área desmarca.
public static class ItemAdministrativoEap
{
    public const string CodigoPacote = "ADM";
    public const string CodigoAtividade = "ADM.1";
    public const string NomeGhe = "Administrativo";

    // "Canteiro" sozinho não conta: mobilização e instalação de canteiro são frente de obra.
    private static readonly string[] TermosAdministrativos = { "administrativ", "escritorio", "almoxarifado", "apontamento" };

    // Item da área administrativa: o padrão (código ADM) ou um incluído à mão com esse nome, como em rascunho
    // anterior ao item padrão (29/10/2026).
    public static bool EhAdministrativo(ItemEapGerado i)
    {
        if (i.Codigo.StartsWith(CodigoPacote, StringComparison.OrdinalIgnoreCase)) return true;
        var nome = AAHBRANT.SST.Application.Common.FuncaoSstClassifier.Normalizar(i.Nome);
        return TermosAdministrativos.Any(nome.Contains);
    }

    public static void Garantir(EstruturaGeracaoPgr e)
    {
        if (e.Itens.Any(i => i.Codigo == CodigoPacote || i.Codigo == CodigoAtividade)) return;
        e.Itens.InsertRange(0, new[]
        {
            new ItemEapGerado { Codigo = CodigoPacote, Nome = "Administrativo / Canteiro", Nivel = 1, Periodo = "Toda a obra" },
            new ItemEapGerado { Codigo = CodigoAtividade, Nome = "Escritório da obra, almoxarifado e apontamento", Nivel = 2, Periodo = "Toda a obra" },
        });
    }
}

public class GheGerado
{
    public int Numero { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Ambiente { get; set; }
    public string? Jornada { get; set; }
    public List<FuncaoGerada> Funcoes { get; set; } = new();
    public List<AtividadeGerada> Atividades { get; set; } = new();
}

public class FuncaoGerada
{
    // Função do cadastro (G-RH) quando casou; nula = função nova, criada na aprovação.
    public Guid? FuncaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    // Descrição das atividades da função — vai na íntegra para a Ordem de Serviço.
    public string? Descricao { get; set; }
    public bool SugeridaPelaIa { get; set; } = true;
    // CBO da tabela oficial (29/10/2026): 6 dígitos quando válido; o valor antigo do cadastro, como veio, quando não está
    // na tabela (a tela mostra "fora da tabela oficial"). Vai para o cadastro de funções na aprovação.
    public string? Cbo { get; set; }
    // Título oficial do código; o servidor preenche ao abrir. Nulo com Cbo preenchido = código fora da tabela.
    public string? CboTitulo { get; set; }
    public bool CboSugeridoPelaIa { get; set; }
}

// O único ajudante genérico do PGR é o "Ajudante Geral" (decisão do usuário, 29/10/2026): Servente, Ajudante/Servente,
// Ajudante de obras etc. viram ele, em todo GHE, e só a descrição muda de um GHE para outro. Ajudante de um ofício
// (de eletricista, de topógrafo...) continua sendo função própria.
public static class AjudanteGeral
{
    public const string Nome = "Ajudante Geral";
    // Servente de obras na CBO 2002: o candidato natural do Ajudante Geral (a IA ainda confirma).
    public const string CboServenteDeObras = "717020";

    private static readonly HashSet<string> PalavrasGenericas = new()
    {
        "ajudante", "auxiliar", "geral", "gerais", "de", "da", "do", "obra", "obras", "servico", "servicos", "construcao", "civil",
    };

    private static readonly string[] Execucao = { "opera ", "operar ", "dirige", "dirigir", "conduz", "pilota", "solda ", "soldar" };

    // O Ajudante Geral só auxilia (orientação do técnico, 29/10/2026, contra desvio de função): a descrição começa com
    // "Auxilia" e não traz atividade que exige habilitação. Nulo quando está certo; senão o motivo, para a pendência.
    public static string? ProblemaDescricao(string? descricao)
    {
        var d = Common.FuncaoSstClassifier.Normalizar(descricao ?? string.Empty);
        if (d.Length == 0) return null;
        if (!d.StartsWith("auxilia")) return "com descrição que não começa com \"Auxilia\" (ele só auxilia, não executa).";
        var termo = Execucao.FirstOrDefault(x => $" {d} ".Contains($" {x}"));
        return termo is null ? null : $"com atividade que exige habilitação na descrição (\"{termo.Trim()}\").";
    }

    public static bool EhGenerico(string? nome)
    {
        var palavras = Common.FuncaoSstClassifier.Normalizar(nome ?? string.Empty)
            .Split(new[] { ' ', '/', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0) return false;
        if (palavras.Any(p => p.StartsWith("servente"))) return true;
        return palavras.Any(p => p is "ajudante" or "auxiliar") && palavras.All(PalavrasGenericas.Contains);
    }

    // Renomeia o ajudante genérico de cada GHE, liga ao "Ajudante Geral" do cadastro (quando existe) e deixa um só por
    // GHE, com a primeira descrição preenchida.
    public static void Unificar(EstruturaGeracaoPgr e, Guid? idCadastro)
    {
        foreach (var g in e.Ghes)
        {
            var ajudantes = g.Funcoes.Where(f => EhGenerico(f.Nome)).ToList();
            if (ajudantes.Count == 0) continue;
            var fica = ajudantes[0];
            if (string.IsNullOrWhiteSpace(fica.Descricao))
                fica.Descricao = ajudantes.Select(f => f.Descricao).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            fica.Cbo ??= ajudantes.Select(f => f.Cbo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            fica.Nome = Nome;
            fica.FuncaoId = idCadastro;
            foreach (var outro in ajudantes.Skip(1)) g.Funcoes.Remove(outro);
        }

        // Um só CBO para o Ajudante Geral em todos os GHEs (orientação do técnico, 29/10/2026): vale o mais usado entre os
        // GHEs; no empate, o do primeiro GHE.
        var todos = e.Ghes.SelectMany(g => g.Funcoes).Where(f => f.Nome == Nome).ToList();
        var escolhido = todos.Where(f => !string.IsNullOrWhiteSpace(f.Cbo))
            .GroupBy(f => CodigoCbo.Normalizar(f.Cbo) ?? f.Cbo!.Trim())
            .OrderByDescending(x => x.Count()).Select(x => x.First()).FirstOrDefault();
        if (escolhido is null) return;
        foreach (var f in todos)
        {
            f.Cbo = escolhido.Cbo;
            f.CboTitulo = escolhido.CboTitulo;
            f.CboSugeridoPelaIa = escolhido.CboSugeridoPelaIa;
        }
    }

    public static async Task<Guid?> IdNoCadastroAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.Funcoes.Select(f => new { f.Id, f.Nome }).ToListAsync(ct))
            .FirstOrDefault(f => Common.FuncaoSstClassifier.Normalizar(f.Nome) == Common.FuncaoSstClassifier.Normalizar(Nome))?.Id;

    // Também padroniza os nomes das demais funções (NomesFuncoes), para valer nos mesmos pontos: sugestão, tela, envio,
    // aprovação e prévia.
    public static async Task UnificarAsync(IAppDbContext db, EstruturaGeracaoPgr e, CancellationToken ct)
    {
        NomesFuncoes.Padronizar(e);
        if (e.Ghes.SelectMany(g => g.Funcoes).Any(f => EhGenerico(f.Nome)))
            Unificar(e, await IdNoCadastroAsync(db, ct));
    }
}

// Nomes padronizados das funções do PGR (orientação do técnico, 29/10/2026), aplicados depois da IA e a cada abertura:
// - caixa alta vira iniciais maiúsculas ("AUXILIAR DE ALMOXARIFE" → "Auxiliar de Almoxarife");
// - "Encarregado" sozinho vira "Encarregado de <GHE>", porque o encarregado é do serviço daquele GHE;
// - a mesma função tem o mesmo nome em todos os GHEs ("Greidista" e "Greidista / Nivelador" viram o nome mais usado).
public static class NomesFuncoes
{
    private static readonly HashSet<string> Minusculas = new() { "de", "da", "do", "das", "dos", "e", "em", "a", "o", "para", "com" };

    public static string Capitalizar(string nome)
    {
        var limpo = string.Join(' ', nome.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var letras = limpo.Where(char.IsLetter).ToList();
        if (letras.Count == 0 || letras.Any(char.IsLower)) return limpo;
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        var palavras = limpo.ToLower(cultura).Split(' ');
        for (var i = 0; i < palavras.Length; i++)
            if (i == 0 || !Minusculas.Contains(palavras[i]))
                palavras[i] = char.ToUpper(palavras[i][0], cultura) + palavras[i][1..];
        return string.Join(' ', palavras);
    }

    // Chave da "mesma função": o nome sem o complemento depois da barra ("Greidista / Nivelador" → "greidista").
    private static string Chave(string nome)
    {
        var barra = nome.IndexOf(" / ", StringComparison.Ordinal);
        return Common.FuncaoSstClassifier.Normalizar(barra > 0 ? nome[..barra] : nome);
    }

    public static void Padronizar(EstruturaGeracaoPgr e)
    {
        foreach (var g in e.Ghes)
            foreach (var f in g.Funcoes)
            {
                f.Nome = Capitalizar(f.Nome);
                if (Common.FuncaoSstClassifier.Normalizar(f.Nome) == "encarregado" && !string.IsNullOrWhiteSpace(g.Nome))
                {
                    f.Nome = $"Encarregado de {Capitalizar(g.Nome.Trim())}";
                    f.FuncaoId = null;
                }
            }

        foreach (var grupo in e.Ghes.SelectMany(g => g.Funcoes).GroupBy(f => Chave(f.Nome)).Where(x => x.Select(f => f.Nome).Distinct().Count() > 1))
        {
            var nome = grupo.GroupBy(f => f.Nome).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Length).First().Key;
            var id = grupo.FirstOrDefault(f => f.Nome == nome && f.FuncaoId != null)?.FuncaoId;
            foreach (var f in grupo) { f.Nome = nome; f.FuncaoId = id; }
        }
    }
}

// Completa e confere o CBO das funções da geração com a tabela oficial. Função sem CBO que casou com o cadastro herda
// o CBO dele (se o cadastro tiver um válido ou não); o código válido fica com 6 dígitos e o título oficial.
public static class CboFuncoesGeracao
{
    public static void Completar(EstruturaGeracaoPgr e, IReadOnlyDictionary<Guid, string?> cboCadastroPorId, CatalogoCbo catalogo)
    {
        foreach (var f in e.Ghes.SelectMany(g => g.Funcoes))
        {
            if (string.IsNullOrWhiteSpace(f.Cbo) && f.FuncaoId is { } id && cboCadastroPorId.GetValueOrDefault(id) is { Length: > 0 } doCadastro)
            {
                f.Cbo = doCadastro.Trim();
                f.CboSugeridoPelaIa = false;
            }
            if (string.IsNullOrWhiteSpace(f.Cbo)) { f.Cbo = null; f.CboTitulo = null; continue; }
            if (catalogo.Existe(f.Cbo))
            {
                f.Cbo = CodigoCbo.Normalizar(f.Cbo);
                f.CboTitulo = catalogo.TituloOficial(f.Cbo);
            }
            else f.CboTitulo = null;
        }
    }

    public static async Task CompletarAsync(IAppDbContext db, EstruturaGeracaoPgr e, CatalogoCbo catalogo, CancellationToken ct)
    {
        var ids = e.Ghes.SelectMany(g => g.Funcoes).Where(f => f.FuncaoId != null).Select(f => f.FuncaoId!.Value).Distinct().ToList();
        var cadastro = await db.Funcoes.Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.CboCodigo, ct);
        Completar(e, cadastro, catalogo);
    }

    // Funções sem CBO da tabela oficial: travam o envio e a aprovação (decisão do usuário, 29/10/2026). Catálogo vazio
    // (tabela ainda não carregada) não trava, para não parar o PGR por falta de dado de sistema.
    public static List<string> Pendencias(EstruturaGeracaoPgr e, CatalogoCbo catalogo)
    {
        var p = new List<string>();
        if (catalogo.Vazio) return p;
        foreach (var g in e.Ghes)
            foreach (var f in g.Funcoes)
            {
                if (string.IsNullOrWhiteSpace(f.Cbo)) p.Add($"GHE {g.Numero:00}: função {f.Nome} sem CBO.");
                else if (!catalogo.Existe(f.Cbo)) p.Add($"GHE {g.Numero:00}: CBO \"{f.Cbo}\" da função {f.Nome} não está na tabela oficial.");
            }
        return p;
    }
}

public class AtividadeGerada
{
    public string CodigoEap { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    // true depois que a IA gerou os riscos (mesmo que nenhum).
    public bool RiscosGerados { get; set; }
    public List<RiscoGerado> Riscos { get; set; } = new();
}

public class RiscoGerado
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    // Físico, Químico, Biológico, Ergonômico ou Acidente.
    public string Tipo { get; set; } = string.Empty;
    public string Perigo { get; set; } = string.Empty;
    public string? Danos { get; set; }
    public int Probabilidade { get; set; }
    public int Severidade { get; set; }
    public List<string> Base { get; set; } = new();
    public List<MedidaGerada> Medidas { get; set; } = new();
    // O engenheiro (ou o técnico, antes de enviar) conferiu P, S, danos e o texto do risco.
    public bool Revisado { get; set; }
    public bool Excluido { get; set; }
    // Como o risco foi avaliado e as medições feitas (ruído, calor, poeira...). Preenchido pelo técnico: a IA não mede nada.
    public ExposicaoRisco Exposicao { get; set; } = new();
}

public static class CriteriosExposicao
{
    public const string Qualitativo = "Qualitativo";
    public const string Quantitativo = "Quantitativo";
}

// Exposição do risco no documento (24/10/2026): critério de avaliação, perfil de exposição e, quando
// quantitativo, as medições com método, equipamento, data e resultado.
public class ExposicaoRisco
{
    public string Criterio { get; set; } = CriteriosExposicao.Qualitativo;
    // Quem se expõe, com que frequência e por quanto tempo (texto livre do técnico).
    public string? Perfil { get; set; }
    public List<MedicaoExposicao> Medicoes { get; set; } = new();
}

public class MedicaoExposicao
{
    public DateTime? Data { get; set; }
    public decimal? Valor { get; set; }
    // Unidade do valor: dB(A) para ruído, °C (IBUTG) para calor, m/s² para vibração, mg/m³ para poeira...
    public string? Unidade { get; set; }
    // Limite de tolerância e nível de ação, com a unidade (ex.: "85 dB(A)"); o técnico confere na NR-15 e nas NHO.
    public string? LimiteTolerancia { get; set; }
    public string? NivelAcao { get; set; }
    public string? Empresa { get; set; }
    public string? Tecnica { get; set; }
    public string? Equipamento { get; set; }
}

// EPI do catálogo, com o CA como está hoje no cadastro (a validade é conferida na aprovação e no PDF).
public record CatalogoEpiInfo(string Nome, string? Ca, DateTime? CaValidade);

// Cronograma mensal do plano de ação: meses do ano em que cada ação está em andamento, de Início a Prazo.
public static class CronogramaPlano
{
    // Sem prazo a ação não entra no cronograma. Sem início, marca só o mês do prazo.
    public static bool[] MesesAtivos(DateTime? inicio, DateTime? prazo, int ano)
    {
        var meses = new bool[12];
        if (prazo is null) return meses;
        var fim = prazo.Value;
        var comeco = inicio is { } i && i <= fim ? i : fim;
        for (var m = 1; m <= 12; m++)
        {
            var primeiroDia = new DateTime(ano, m, 1);
            var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);
            meses[m - 1] = comeco.Date <= ultimoDia && fim.Date >= primeiroDia;
        }
        return meses;
    }
}

public class MedidaGerada
{
    // 1 Antes, 2 Durante, 3 Após (mesmos valores de FaseMedidaPrevencao).
    public int Fase { get; set; } = 2;
    public string Texto { get; set; } = string.Empty;
    // Hierarquia da NR-01: "Eliminação", "EPC", "Administrativa" ou "EPI". Nulo nos rascunhos antigos: o
    // engenheiro classifica antes de aprovar. Só vive no rascunho e no PDF; a medida da Ordem de Serviço
    // não guarda categoria.
    public string? Categoria { get; set; }
    // EPI do catálogo (só nas medidas da categoria EPI): leva o CA válido ao documento (NR-06).
    public Guid? CatalogoEpiId { get; set; }
    // "Pendente", "Aceita" ou "Descartada".
    public string Decisao { get; set; } = "Pendente";

    // Regra do padrão (29/10/2026): o que a IA sugeriu e ninguém decidiu vale como aceito, na prévia, no PDF e na
    // aprovação. Só a medida descartada fica de fora.
    public bool EntraNoPgr() => Decisao != "Descartada";
}
