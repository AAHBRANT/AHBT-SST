using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Novidades;

public class NovidadeVersaoItemDto
{
    public Guid Id { get; set; }
    public CategoriaNovidade Categoria { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Agora { get; set; }
    public int Ordem { get; set; }
}

public class NovidadeVersaoDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Versao { get; set; } = string.Empty;
    public DateTime DataPublicacao { get; set; }
    public List<NovidadeVersaoItemDto> Itens { get; set; } = new();
}

public class NovidadeVersaoItemInput
{
    public CategoriaNovidade Categoria { get; set; } = CategoriaNovidade.Melhoria;
    public string Descricao { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Agora { get; set; }
}
