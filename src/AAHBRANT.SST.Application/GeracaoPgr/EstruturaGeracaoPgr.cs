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
}
