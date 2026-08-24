using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// NTAG.md §2 — sst_areas. "construction_site_id ... -- Vínculo com a Obra/Unidade" (comentário
// literal do documento, que não decide entre os dois): modelada aqui como ObraId — mesma
// granularidade física já usada por Atividade/Trabalhador em todo o resto do sistema. Decisão de
// mapeamento, não citação exata do documento.
public class AreaSst : AuditableEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoArea Tipo { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string? DetalhesLocalizacao { get; set; }

    // "Riscos e Requisitos (Armazenados como JSONB para flexibilidade)" — literal do documento.
    // Traduzido para coluna JSON (nvarchar(max) + conversor) no SQL Server, preservando a mesma
    // flexibilidade de lista livre de strings descrita no schema Postgres original.
    public List<string> Riscos { get; set; } = new();
    public List<string> Requisitos { get; set; } = new();

    public StatusArea Status { get; set; } = StatusArea.Ativa;
}
