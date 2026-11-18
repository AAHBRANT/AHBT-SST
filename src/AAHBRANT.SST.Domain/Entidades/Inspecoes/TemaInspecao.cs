using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Tema de inspeção criado pelo usuário (ex.: "Andaime"), ao lado dos fixos Alojamento e Veículos.
// Cada tema tem um checklist próprio (ChecklistModelo com TemaInspecaoId) e os itens cadastrados por
// obra (ItemInspecionavel), cada um com o seu histórico de inspeções (Inspecao.ItemInspecionavelId).
public class TemaInspecao : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;

    // Periodicidade sugerida para os itens novos deste tema (cada item pode ter a sua).
    public int? PeriodicidadeDiasPadrao { get; set; }

    // Selo do adesivo de inspeção quando o item do tema passa na inspeção (17/11/2026): Liberado (objeto de
    // uso/operação) ou Inspecionado (local/dispositivo). Com item Não Conforme o adesivo é NÃO LIBERADO.
    public AAHBRANT.SST.Domain.Enums.ModeloAdesivoInspecao ModeloAdesivo { get; set; } = AAHBRANT.SST.Domain.Enums.ModeloAdesivoInspecao.Liberado;
}

// Item concreto a inspecionar dentro de um tema (ex.: "Andaime fachada norte"). Mudar de obra é
// permitido — o histórico acompanha o item, como no Veículo.
public class ItemInspecionavel : AuditableEntity
{
    public Guid TemaInspecaoId { get; set; }
    public TemaInspecao? TemaInspecao { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // Como o item é reconhecido em campo (ex.: "Andaime A-03 — fachada norte").
    public string Identificacao { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    // Inspeção periódica: ver Veiculo.PeriodicidadeInspecaoDias.
    public int? PeriodicidadeInspecaoDias { get; set; }
    public Guid? ResponsavelInspecaoUsuarioId { get; set; }
    public Usuario? ResponsavelInspecaoUsuario { get; set; }
}
