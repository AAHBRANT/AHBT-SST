using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// EPC — Equipamento de Proteção Coletiva (pedido do usuário, 04/09): módulo próprio, separado do
// EPI. Decisão confirmada com o usuário: EPC não é "entregue" a um funcionário como o EPI (não tem
// assinatura de recebimento) — fica instalado numa Obra, com validade e inspeções periódicas
// ("Instalação/Inspeção por Obra"), e sem matriz de obrigatoriedade (isso ficou de fora de propósito).
public class CatalogoEpc : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Fabricante { get; set; }
    // Nem todo EPC tem Certificado de Aprovação (ex.: sinalização não tem; guarda-corpo/redes de
    // proteção às vezes têm) — opcional, ao contrário do CA de EPI que é sempre preenchido na prática.
    public string? CertificadoAprovacaoNumero { get; set; }
    public DateTime? CertificadoAprovacaoValidade { get; set; }
    public int VidaUtilEmMeses { get; set; }

    public byte[]? FotoConteudo { get; set; }
    public string? FotoContentType { get; set; }

    public ICollection<InstalacaoEpc> Instalacoes { get; set; } = new List<InstalacaoEpc>();
    public ICollection<EstoqueEpc> Estoques { get; set; } = new List<EstoqueEpc>();
}

// Registro de um EPC instalado numa Obra — o equivalente a "Entrega" no módulo EPI, mas sem
// trabalhador nem assinatura. "Última inspeção" fica direto aqui (não numa tabela filha à parte) —
// decisão não-literal assumida para não criar uma sub-tela de agenda de inspeções que não foi pedida.
public class InstalacaoEpc : AuditableEntity
{
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string? LocalInstalacao { get; set; } // texto livre, ex.: "Torre 2, pavimento 8"
    public int Quantidade { get; set; } = 1;

    public DateTime DataInstalacao { get; set; }
    public DateTime? DataValidade { get; set; }

    public DateTime? DataUltimaInspecao { get; set; }
    public StatusInspecaoEpc? StatusUltimaInspecao { get; set; }
    public string? ObservacoesInspecao { get; set; }

    // Removido/desinstalado — repõe o estoque da Obra (mesmo princípio da devolução de EntregaEpi).
    public DateTime? DataRemocao { get; set; }
    public string? Observacoes { get; set; }
}

public class EstoqueEpc : AuditableEntity
{
    public Guid CatalogoEpcId { get; set; }
    public CatalogoEpc? CatalogoEpc { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public int Saldo { get; set; }

    public ICollection<MovimentacaoEstoqueEpc> Movimentacoes { get; set; } = new List<MovimentacaoEstoqueEpc>();
}

public class MovimentacaoEstoqueEpc : AuditableEntity
{
    public Guid EstoqueEpcId { get; set; }
    public EstoqueEpc? EstoqueEpc { get; set; }
    public TipoMovimentacaoEstoqueEpc Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoResultante { get; set; }
    public Guid? InstalacaoEpcId { get; set; }
    public InstalacaoEpc? InstalacaoEpc { get; set; }
    public string? Observacao { get; set; }
}
