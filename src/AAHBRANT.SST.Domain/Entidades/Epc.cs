using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo EPC (Equipamento de Proteção Coletiva) — mesmo padrão arquitetural de EPI/Uniforme
// (decisão do usuário, 2026-09-07: "os 3 devem ser bem parecidos"), sem dimensão de tamanho — EPC
// não é uma peça vestível, então o estoque é só por Obra, sem grade. Este catálogo é distinto do
// checklist fixo ItemEpcPt (§5 da Permissão de Trabalho): aquele é uma lista fechada de categorias
// verificadas por PT ("Isolamento/Barreira", "Sinalização" etc.), sem itens individuais cadastrados;
// este é um catálogo de itens de verdade (nome, foto, estoque, entrega rastreada a um responsável).
public class CatalogoEpc : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }

    // Foto do item cadastrado — mesmo padrão de CatalogoEpi.FotoConteudo/CatalogoUniforme.FotoConteudo.
    public byte[]? FotoConteudo { get; set; }
    public string? FotoContentType { get; set; }

    public ICollection<EstoqueEpc> Estoques { get; set; } = new List<EstoqueEpc>();
    public ICollection<EntregaEpc> Entregas { get; set; } = new List<EntregaEpc>();
}

// Estoque segmentado por Obra apenas (sem Tamanho, diferente de EstoqueUniforme) — mesmo princípio
// de EstoqueEpi (Fase 3).
public class EstoqueEpc : AuditableEntity
{
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public int Saldo { get; set; }

    public ICollection<MovimentacaoEstoqueEpc> Movimentacoes { get; set; } = new List<MovimentacaoEstoqueEpc>();
}

// Ledger append-only de movimentações — mesmo papel de MovimentacaoEstoqueUniforme. Sem
// DevolucaoEntrada: não há fluxo de devolução de EPC neste módulo.
public class MovimentacaoEstoqueEpc : AuditableEntity
{
    public Guid EstoqueEpcId { get; set; }
    public EstoqueEpc? EstoqueEpc { get; set; }
    public TipoMovimentacaoEstoqueEpc Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoResultante { get; set; }
    public Guid? EntregaEpcId { get; set; }
    public EntregaEpc? EntregaEpc { get; set; }
    public string? Observacao { get; set; }
}

// Autorização por função — mesmo formato de MatrizEpiFuncao/MatrizUniformeFuncao.
public class MatrizEpcFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }
}

// Registro de entrega — mesmo formato enxuto de EntregaUniforme, sem o campo Tamanho (EPC não tem
// tamanho). Diferente de EPI, o "responsável" aqui não necessariamente usa o item sozinho (é
// proteção coletiva), mas o registro de entrega ainda rastreia a quem o item foi atribuído/
// entregue, mesmo princípio de responsabilização já usado em EPI/Uniforme.
public class EntregaEpc : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }
    public int Quantidade { get; set; } = 1;
    public DateTime DataEntrega { get; set; }
    public MotivoEntregaEpc MotivoTipo { get; set; }
    public string? Observacoes { get; set; }
}
