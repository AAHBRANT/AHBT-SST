namespace AAHBRANT.SST.Application.CatalogosUniforme;

public record CatalogoUniformeDto(Guid Id, string Nome, string? Categoria, bool TemFoto, string? ReferenciaVisual = null, List<string>? FuncoesAtribuidas = null);
