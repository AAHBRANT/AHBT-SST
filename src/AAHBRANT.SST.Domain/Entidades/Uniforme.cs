using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo Uniforme — mesmo padrão arquitetural do EPI (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md), com uma diferença central: uniforme tem tamanho (camisa P/M/G/GG, calça/
// bota por numeração). CatalogoUniforme representa só a peça (ex.: "Camisa"), sem tamanho embutido;
// o tamanho vira uma dimensão do estoque (grade por Obra+Tamanho) e do trabalhador (tamanho
// individual), nunca do catálogo.
public class CatalogoUniforme : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }

    public ICollection<EstoqueUniforme> Estoques { get; set; } = new List<EstoqueUniforme>();
    public ICollection<EntregaUniforme> Entregas { get; set; } = new List<EntregaUniforme>();
}

// Grade de estoque: uma linha por (CatalogoUniformeId, ObraId, Tamanho) — Tamanho é texto livre
// porque a granularidade varia por peça (P/M/G/GG para camisa, numeração para calça/bota), sem
// enum fixo. Mesmo princípio de segmentação por Obra já usado em EstoqueEpi (Fase 3 do EPI).
public class EstoqueUniforme : AuditableEntity
{
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Tamanho { get; set; } = string.Empty;
    public int Saldo { get; set; }

    public ICollection<MovimentacaoEstoqueUniforme> Movimentacoes { get; set; } = new List<MovimentacaoEstoqueUniforme>();
}

// Ledger append-only de movimentações — mesmo papel de MovimentacaoEstoqueEpi. Sem
// DevolucaoEntrada: não há fluxo de devolução de uniforme neste módulo (fora de escopo, ver spec).
public class MovimentacaoEstoqueUniforme : AuditableEntity
{
    public Guid EstoqueUniformeId { get; set; }
    public EstoqueUniforme? EstoqueUniforme { get; set; }
    public TipoMovimentacaoEstoqueUniforme Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoResultante { get; set; }
    public Guid? EntregaUniformeId { get; set; }
    public EntregaUniforme? EntregaUniforme { get; set; }
    public string? Observacao { get; set; }
}

// Autorização por função — aponta só para a peça, sem fixar tamanho (o tamanho vem do trabalhador
// que está recebendo, resolvido em tempo de entrega). Mesmo formato de MatrizEpiFuncao.
public class MatrizUniformeFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
}

// Tamanho de cada trabalhador, por peça — tabela própria (não campos fixos tipo TamanhoCamisa no
// cadastro do trabalhador) para que uma peça nova no catálogo não exija alterar o cadastro do
// trabalhador de novo (decisão do brainstorming, 2026-09-07).
public class TrabalhadorTamanhoUniforme : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public string Tamanho { get; set; } = string.Empty;
}

// Registro de entrega — mais enxuto que EntregaEpi (sem VistoConsorcioResponsavel/NR-6, que são
// campos específicos da ficha oficial de EPI; sem DataDevolucao/QuantidadeDevolucao, pois não há
// fluxo de devolução de uniforme). Tamanho é um snapshot no momento da entrega (não referência viva
// a TrabalhadorTamanhoUniforme) — se o trabalhador trocar de tamanho depois, o histórico de
// entregas antigas não muda retroativamente.
public class EntregaUniforme : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoUniformeId { get; set; }
    public CatalogoUniforme? CatalogoUniforme { get; set; }
    public string Tamanho { get; set; } = string.Empty;
    public int Quantidade { get; set; } = 1;
    public DateTime DataEntrega { get; set; }
    public MotivoEntregaUniforme MotivoTipo { get; set; }
    public string? Observacoes { get; set; }
}
