using System.Globalization;
using System.Text;

namespace AAHBRANT.SST.Application.Ideias;

public class HeuristicaIdeiaEstruturacaoService : IIdeiaEstruturacaoService
{
    // Módulos do app (ver ONBOARDING §4) com palavras-chave. Ordem importa só no desempate.
    private static readonly (string Modulo, string[] Termos)[] Modulos =
    [
        ("EPI", ["epi", "capacete", "luva", "bota", "ficha de epi", "equipamento de protecao"]),
        ("Saúde Ocupacional", ["aso", "pcmso", "exame", "atestado", "saude ocupacional", "medico"]),
        ("Treinamentos", ["treinamento", "capacitacao", "certificado", "curso", "nr-35", "nr35"]),
        ("DDS", ["dds", "dialogo diario", "seguranca do dia"]),
        ("Inspeções", ["inspecao", "checklist", "vistoria"]),
        ("Não Conformidades", ["nao conformidade", "nao-conformidade", "causa raiz", "acao corretiva"]),
        ("Acidentes e Incidentes", ["acidente", "incidente", "quase acidente", "cat ", "investigacao"]),
        ("APR / PT", ["apr", "permissao de trabalho", "analise preliminar"]),
        ("PGR / Riscos", ["pgr", "risco", "perigo", "matriz de risco"]),
        ("CIPA", ["cipa", "sipat"]),
        ("Alertas", ["alerta", "aviso", "notificacao", "vencimento", "vencido", "vencer"]),
        ("Compras", ["compra", "pedido", "fornecedor", "material recebido", "requisicao"]),
        ("Pessoas", ["trabalhador", "funcionario", "colaborador", "cadastro de pessoa"]),
        ("Obras", ["obra", "canteiro", "alojamento"]),
        ("Administração", ["usuario", "perfil de acesso", "permissao de acesso", "rbac"]),
    ];

    private static readonly (string Categoria, string[] Termos)[] Categorias =
    [
        ("Inteligência Artificial", ["ia ", " ia", "inteligencia artificial", "automaticamente", "sugerir", "sugira", "analisar automaticamente"]),
        ("Alertas e Notificações", ["avisar", "alerta", "notificar", "notificacao", "lembrete"]),
        ("Integração", ["integrar", "integracao", "api", "teams", "telegram", "whatsapp", "sincronizar"]),
        ("Relatórios e Indicadores", ["relatorio", "dashboard", "indicador", "grafico", "exportar"]),
        ("Usabilidade", ["tela", "botao", "facilitar", "mais rapido", "simplificar", "layout"]),
        ("Mobile / Campo", ["celular", "offline", "campo", "app", "aplicativo", "foto"]),
        ("Novo Recurso", ["criar", "novo", "nova", "adicionar", "incluir"]),
    ];

    private static readonly string[] IntegracoesTermos = ["teams", "telegram", "whatsapp", "email", "e-mail", "api", "grh", "sap", "calendario", "outlook"];

    public Task<IdeiaEstruturada> EstruturarAsync(string mensagem, CancellationToken ct)
    {
        var texto = mensagem.Trim();
        var norm = Normalizar(texto);

        var modulo = Melhor(Modulos, norm);
        var categoria = Melhor(Categorias, norm);
        var necessidadeIa = norm.Contains("inteligencia artificial") || norm.Contains(" ia ") || norm.StartsWith("ia ")
            || norm.Contains("automaticamente") || norm.Contains("sugerir") || norm.Contains("sugira");
        var integracoes = IntegracoesTermos.Where(norm.Contains).Select(t => t.ToUpperInvariant()).Distinct().ToList();

        var faltantes = new List<string>();
        if (modulo is null) faltantes.Add("Módulo/área do sistema relacionado");
        if (texto.Length < 60) faltantes.Add("Detalhes da situação atual (qual problema isto resolve?)");
        if (!norm.Contains("para ") && !norm.Contains("pois") && !norm.Contains("porque") && !norm.Contains("evitar"))
            faltantes.Add("Benefício esperado");

        var estruturada = new IdeiaEstruturada(
            Titulo: GerarTitulo(texto),
            Descricao: texto,
            ProblemaOportunidade: null,
            Objetivo: null,
            SolucaoSugerida: texto,
            Modulo: modulo,
            Categoria: categoria,
            BeneficioEsperado: null,
            PossiveisImpactos: modulo is null ? null : $"Pode exigir ajustes no módulo {modulo}.",
            IntegracoesNecessarias: integracoes.Count == 0 ? null : string.Join(", ", integracoes),
            NecessidadeIa: necessidadeIa ? true : null,
            InformacoesFaltantes: faltantes.Count == 0 ? null : string.Join("; ", faltantes),
            EstruturadoPor: "Heuristica");

        return Task.FromResult(estruturada);
    }

    internal static string GerarTitulo(string texto)
    {
        var primeira = texto.Split(['\n', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).FirstOrDefault() ?? texto;
        primeira = primeira.Trim('"', '“', '”', ' ');
        if (primeira.Length == 0) primeira = "Ideia sem título";
        if (primeira.Length > 90)
        {
            var corte = primeira.LastIndexOf(' ', 90);
            primeira = primeira[..(corte > 40 ? corte : 90)].TrimEnd(',', ';', ' ') + "…";
        }
        return char.ToUpper(primeira[0], CultureInfo.GetCultureInfo("pt-BR")) + primeira[1..];
    }

    private static string? Melhor((string Nome, string[] Termos)[] opcoes, string norm)
    {
        string? melhor = null;
        var pontos = 0;
        foreach (var (nome, termos) in opcoes)
        {
            var p = termos.Count(t => norm.Contains(t));
            if (p > pontos) { pontos = p; melhor = nome; }
        }
        return melhor;
    }

    // Minúsculas, sem acento, com espaços nas pontas (permite casar " ia " de forma simples).
    internal static string Normalizar(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(" ");
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        sb.Append(' ');
        return sb.ToString();
    }
}
