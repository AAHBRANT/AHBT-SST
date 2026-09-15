using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Alojamento cadastrado por obra — hoje só manual; GrhAlojamentoId/DataUltimaSincronizacao ficam
// nulos até a integração com o G-RH existir (ver docs/superpowers/2026-09-15-pedido-integracao-
// alojamento-grh.md). Quando a sync existir, Origem (herdado de AuditableEntity) marca se o
// registro veio de lá.
public class Alojamento : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public string? GrhAlojamentoId { get; set; }
    public DateTime? DataUltimaSincronizacao { get; set; }

    public ICollection<AlojamentoMorador> Moradores { get; set; } = new List<AlojamentoMorador>();
}

// Vínculo morador-alojamento. DataSaida nula = morador atual. Índice único (ver
// AlojamentoConfiguracoes.cs) garante que um mesmo TrabalhadorId nunca tenha dois vínculos ativos
// ao mesmo tempo — regra de negócio, não só otimização.
public class AlojamentoMorador : AuditableEntity
{
    public Guid AlojamentoId { get; set; }
    public Alojamento? Alojamento { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public DateTime DataDesde { get; set; }
    public DateTime? DataSaida { get; set; }
}

// Configuração global (linha única) — "quantos dias sem inspeção concluída até um alojamento virar
// 'atrasado' nos cards". Nunca hardcoded no front, editável via tela de Administração (Task 4).
public class ConfiguracaoAlojamento : AuditableEntity
{
    public int DiasParaInspecaoAtrasada { get; set; } = 30;
}
