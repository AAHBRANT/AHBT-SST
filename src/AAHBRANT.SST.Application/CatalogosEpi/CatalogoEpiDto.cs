namespace AAHBRANT.SST.Application.CatalogosEpi;

public record CatalogoEpiDto(
    Guid Id,
    string Nome,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses);
