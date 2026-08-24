using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Checklist parametrizável (§24): "diferentes versões do checklist" é requisito literal —
// nova versão gera uma nova linha (ChecklistModeloAnteriorId aponta para a versão anterior),
// nunca sobrescreve a versão existente. Somente uma versão por família fica com Ativo=true.
public class ChecklistModelo : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public TipoInspecao TipoInspecao { get; set; }
    public int Versao { get; set; } = 1;

    public Guid? ChecklistModeloAnteriorId { get; set; }
    public ChecklistModelo? ChecklistModeloAnterior { get; set; }

    public ICollection<ChecklistModeloItem> Itens { get; set; } = new List<ChecklistModeloItem>();
}

// Item do template (§24): conjunto possível de campos por item é conforme/não conforme/não
// aplicável/observação/fotografia/responsável/prazo/evidência — as 3 flags abaixo controlam
// quais desses campos este item específico exige ao ser respondido (nem todo item exige todos).
public class ChecklistModeloItem : AuditableEntity
{
    public Guid ChecklistModeloId { get; set; }
    public ChecklistModelo? ChecklistModelo { get; set; }

    public int Ordem { get; set; }
    public string Descricao { get; set; } = string.Empty;

    public bool ExigeFotografia { get; set; }
    public bool ExigeResponsavel { get; set; }
    public bool ExigePrazo { get; set; }
}
