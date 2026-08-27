using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class CatalogoEpi : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Fabricante { get; set; }
    public string? CertificadoAprovacaoNumero { get; set; } // CA do EPI
    public DateTime? CertificadoAprovacaoValidade { get; set; }
    public int VidaUtilEmMeses { get; set; }

    // Módulo de Controle e Entrega de EPI (especificação fornecida pelo usuário) — saldo simples
    // por item de catálogo, não por lote/obra: decisão própria por não haver estrutura de lote no
    // documento. Cada CriarEntregaEpi
    // decrementa este saldo (bloqueando a entrega se insuficiente, decisão confirmada com o
    // usuário) e cada devolução o incrementa de volta.
    public int SaldoEstoque { get; set; }

    public ICollection<EntregaEpi> Entregas { get; set; } = new List<EntregaEpi>();
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
