namespace AAHBRANT.SST.Application.GeracaoPgr;

// Estrutura de trabalho do assistente "Gerar PGR pela EAP" (21/10/2026), guardada como JSON em
// GeracaoPgrEap.EstruturaJson. A tela edita e devolve o objeto inteiro a cada passo; só a aprovação do
// Engenheiro de Segurança transforma isto em Pgr/Ghe/Atividade/Risco/MedidaPrevencao.
public class EstruturaGeracaoPgr
{
    public List<ItemEapGerado> Itens { get; set; } = new();
    public List<GheGerado> Ghes { get; set; } = new();
    public List<string> PlanoAcao { get; set; } = new();
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
    // "Pendente", "Aceita" ou "Descartada".
    public string Decisao { get; set; } = "Pendente";
}
