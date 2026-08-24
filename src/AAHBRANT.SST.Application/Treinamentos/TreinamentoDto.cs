namespace AAHBRANT.SST.Application.Treinamentos;

public record TreinamentoDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    DateTime DataValidade,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado);
