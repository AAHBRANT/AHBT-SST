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

public record QrCodePerfilPublicoResultado(byte[] Png, string UrlPerfil);

public interface IQrCodePerfilPublicoService
{
    QrCodePerfilPublicoResultado Gerar(string uid);
    string MontarUrl(string uid);
}

public class QrCodeTrabalhadorDto
{
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid TagId { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string UrlPerfil { get; set; } = string.Empty;
    public bool JaExistia { get; set; }
}
