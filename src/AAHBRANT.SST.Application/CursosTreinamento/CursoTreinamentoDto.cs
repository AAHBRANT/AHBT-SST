namespace AAHBRANT.SST.Application.CursosTreinamento;

public record CursoTreinamentoDto(
    Guid Id,
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses);
