namespace AAHBRANT.SST.Application.CatalogosEpc;

public record CatalogoEpcDto(
    Guid Id,
    string Nome,
    string? Fabricante,
    string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade,
    int VidaUtilEmMeses,
    int SaldoTotal,
    bool TemFoto);
