using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class CatalogoEpi : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Fabricante { get; set; }
    public string? CertificadoAprovacaoNumero { get; set; } // CA do EPI
    public DateTime? CertificadoAprovacaoValidade { get; set; }
    public int VidaUtilEmMeses { get; set; }

    public ICollection<EntregaEpi> Entregas { get; set; } = new List<EntregaEpi>();

    // Fase 3 (estoque segmentado por Obra) — substitui o antigo campo SaldoEstoque único e global.
    // Uma linha de EstoqueEpi por (CatalogoEpiId, ObraId); o saldo "total" exibido no catálogo é a
    // soma desta coleção.
    public ICollection<EstoqueEpi> Estoques { get; set; } = new List<EstoqueEpi>();
}

public class EntregaEpi : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }

    public DateTime DataEntrega { get; set; }
    public DateTime? DataDevolucao { get; set; }
    public DateTime? DataValidade { get; set; }

    // Campos da ficha de entrega de EPI (especificação fornecida pelo usuário) além dos já existentes acima.
    public int Quantidade { get; set; } = 1;
    public int? QuantidadeDevolucao { get; set; }
    public string? VistoConsorcioResponsavel { get; set; }
    public string? Motivo { get; set; } // ex.: "Entrega inicial", "Substituição por desgaste"
    public string? Observacoes { get; set; }

    // Ficha de EPI reformulada — MotivoTipo é o campo estruturado exigido pelo modelo oficial
    // (Motivo acima vira observação complementar opcional). Nullable para não quebrar entregas
    // antigas; obrigatório apenas via validação de aplicação em CriarEntregaEpiCommand.
    public MotivoEntregaEpi? MotivoTipo { get; set; }
    public string? NumeroListaPresencaNr6 { get; set; }
    public DateTime? DataTreinamentoNr6 { get; set; }

    // AssinaturaColetada (bool solto) removido: a partir do Motor de Assinatura Eletrônica, o
    // status de assinatura desta entrega passa a ser consultado via DocumentoAssinatura
    // (EntidadeTipo="EntregaEpi", EntidadeId=Id) — mesmo padrão já adotado por PermissaoTrabalho,
    // que nunca teve um campo equivalente na própria entidade.
    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}

public class MatrizEpiFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
}

// Fase 3 da reformulação do módulo EPI — saldo de um CatalogoEpi segmentado por Obra (decisão já
// confirmada com o usuário em sessão anterior: segmentação por Obra apenas, sem Almoxarifado
// separado). Uma linha por (CatalogoEpiId, ObraId); CriarEntregaEpiCommand/AtualizarEntregaEpiCommand
// resolvem a Obra via Trabalhador.ObraId para decrementar/incrementar a linha correta.
public class EstoqueEpi : AuditableEntity
{
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public int Saldo { get; set; }

    public ICollection<MovimentacaoEstoqueEpi> Movimentacoes { get; set; } = new List<MovimentacaoEstoqueEpi>();
}

// Ledger append-only de movimentações de estoque — cada entrada/saída/devolução/ajuste gera uma
// linha com o saldo resultante, dando histórico e auditoria (o antigo CatalogoEpi.SaldoEstoque não
// tinha nenhum). EntregaEpiId é preenchido só para SaidaEntrega/DevolucaoEntrada, que são geradas
// automaticamente pelos commands de EntregaEpi.
public class MovimentacaoEstoqueEpi : AuditableEntity
{
    public Guid EstoqueEpiId { get; set; }
    public EstoqueEpi? EstoqueEpi { get; set; }
    public TipoMovimentacaoEstoqueEpi Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoResultante { get; set; }
    public Guid? EntregaEpiId { get; set; }
    public EntregaEpi? EntregaEpi { get; set; }
    public string? Observacao { get; set; }
}
