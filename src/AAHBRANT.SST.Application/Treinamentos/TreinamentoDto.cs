namespace AAHBRANT.SST.Application.Treinamentos;

public record TreinamentoDto(
    Guid Id,
    Guid TrabalhadorId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    DateTime DataValidade,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado,
    string? Local,
    string? InstrutorRegistroProfissional,
    Guid? SessaoTreinamentoId = null,
    // Perfil do funcionário (07/11/2026): externo sem anexo aparece como "Pendente de anexo".
    Domain.Enums.OrigemCertificadoTreinamento OrigemCertificado = Domain.Enums.OrigemCertificadoTreinamento.Aahbrant,
    bool TemArquivo = false);
