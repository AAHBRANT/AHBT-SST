namespace AAHBRANT.SST.Application.Treinamentos;

public record CertificadoTreinamentoPdfSignatarioModelo(string TrabalhadorNome, DateTime AssinadoEm);

// Certificado individual de conclusão de treinamento/NR (PR-SST-002, item 4). Distinto do
// comprovante genérico de DocumentoAssinaturaPdfService (que só atesta quem assinou o quê) — este
// reproduz o conteúdo do treinamento em si (curso, norma, carga horária, instrutor), no mesmo
// padrão visual AAHBRANT (#670000) usado em EntregaEpiPdfService/DdsPdfService.
public record CertificadoTreinamentoPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    string? ObraCnpj,
    string? ObraEndereco,
    string? ObraCidade,
    string? ObraUf,
    string TrabalhadorNome,
    string TrabalhadorCpfMascarado,
    string? TrabalhadorRg,
    string TrabalhadorFuncaoNome,
    string CursoNome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int CargaHorariaRealizada,
    DateTime DataRealizacao,
    DateTime DataValidade,
    string? InstituicaoInstrutor,
    string? NumeroCertificado,
    string? ConteudoProgramatico,
    IReadOnlyList<CertificadoTreinamentoPdfSignatarioModelo> Signatarios,
    byte[]? QrCodeValidacaoPng,
    byte[]? FotoTurma);

public interface ICertificadoTreinamentoPdfService
{
    byte[] Gerar(CertificadoTreinamentoPdfModelo modelo);
}
