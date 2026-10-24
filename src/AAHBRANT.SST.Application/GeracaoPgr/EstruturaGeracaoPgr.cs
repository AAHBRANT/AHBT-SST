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
    // Formato antigo (uma ação por linha, sem responsável nem prazo). Só existe para ler rascunhos feitos
    // antes de 23/10/2026: Normalizar() passa as linhas para AcoesPlano.
    public List<string> PlanoAcao { get; set; } = new();

    public EstruturaGeracaoPgr Normalizar()
    {
        foreach (var texto in PlanoAcao.Where(t => !string.IsNullOrWhiteSpace(t)))
            AcoesPlano.Add(new AcaoPlanoGerada { Descricao = texto.Trim() });
        PlanoAcao = new();
        return this;
    }
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
    public DateTime? Prazo { get; set; }
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
    // "Pendente", "Aceita" ou "Descartada".
    public string Decisao { get; set; } = "Pendente";
}
