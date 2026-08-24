using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.TagsIdentificacao;

public class TagIdentificacaoDto
{
    public Guid Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public TipoTag Tipo { get; set; }
    public StatusTag Status { get; set; }
    public TipoEntidadeVinculada? EntidadeVinculadaTipo { get; set; }
    public Guid? EntidadeVinculadaId { get; set; }
}

// NTAG.md §1 — "o sistema resolve o identificador e carrega a entidade correspondente no
// contexto correto de SST". EntidadeVinculadaNome só é preenchido quando a entidade vinculada já
// existe no sistema hoje (Area/Trabalhador); Ativo (equipamento) ainda não tem catálogo próprio.
public class ResolverTagDto
{
    public Guid TagId { get; set; }
    public string Uid { get; set; } = string.Empty;
    public TipoTag Tipo { get; set; }
    public StatusTag Status { get; set; }
    public TipoEntidadeVinculada? EntidadeVinculadaTipo { get; set; }
    public Guid? EntidadeVinculadaId { get; set; }
    public string? EntidadeVinculadaNome { get; set; }
}
