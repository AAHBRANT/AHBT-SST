namespace AAHBRANT.SST.Application.Treinamentos;

public record CertificadoTreinamentoPdfSignatarioModelo(
    string TrabalhadorNome,
    DateTime AssinadoEm,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura? Metodo = null,
    string? Funcao = null,
    // SHA-256 da assinatura (signatário + trabalhador + data/hora + método + hash do documento),
    // mostrado no selo do certificado (pedido do usuário, 19/10).
    string? Hash = null);

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
    string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome,
    string CursoNome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int CargaHorariaRealizada,
    DateTime DataRealizacao,
    DateTime DataValidade,
    string? InstituicaoInstrutor,
    string? InstrutorRegistroProfissional,
    string? NumeroCertificado,
    string? Local,
    string? ConteudoProgramatico,
    IReadOnlyList<CertificadoTreinamentoPdfSignatarioModelo> Signatarios,
    byte[]? QrCodeValidacaoPng,
    byte[]? FotoTurma,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura,
    // Número normalizado ("NR-35") e título da norma vindos de CatalogoNormasRegulamentadoras —
    // destaque da frente no modelo de certificado de 19/10.
    string? NormaNumero = null,
    string? NormaTitulo = null,
    // Instrutor do perfil de instrutor (28/10): cargo conforme a formação (Técnico ou Engenheiro de
    // Segurança) e a assinatura dele, quando já assinou. Nulos nas turmas sem instrutor escolhido, que
    // mantêm o texto fixo anterior.
    string? InstrutorCargo = null);

public interface ICertificadoTreinamentoPdfService
{
    byte[] Gerar(CertificadoTreinamentoPdfModelo modelo);
}
