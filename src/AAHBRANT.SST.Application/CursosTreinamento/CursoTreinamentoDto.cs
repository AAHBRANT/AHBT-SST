namespace AAHBRANT.SST.Application.CursosTreinamento;

public record CursoTreinamentoDto(
    Guid Id,
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico,
    bool EhIntegracaoSeguranca,
    // Marcador que habilita a entrega de EPI (22/09) — ver CursoTreinamento.AtendeNr6.
    bool AtendeNr6);
