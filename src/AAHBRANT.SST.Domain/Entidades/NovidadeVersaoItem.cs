using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Cada linha do pop-up de novidades (ver NovidadeVersao). Antes/Agora são opcionais — nem toda
// novidade tem um "como era antes" que valha a pena comparar (ex.: um recurso totalmente novo).
public class NovidadeVersaoItem : AuditableEntity
{
    public Guid NovidadeVersaoId { get; set; }
    public NovidadeVersao? NovidadeVersao { get; set; }

    public CategoriaNovidade Categoria { get; set; } = CategoriaNovidade.Melhoria;
    public string Descricao { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Agora { get; set; }
    public int Ordem { get; set; }
}
