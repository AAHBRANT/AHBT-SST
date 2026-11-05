using AAHBRANT.SST.Application.Assinatura;

namespace AAHBRANT.SST.Application.SessoesTreinamento;

public record AtaSessaoTreinamentoPdfParticipanteModelo(
    string TrabalhadorNome,
    string? TrabalhadorMatricula,
    DateTime? PresencaConfirmadaEm,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura? Metodo = null,
    // 05/11: função com CBO (coluna da lista) e selo da assinatura na linha.
    string? FuncaoComCbo = null,
    SeloAssinaturaPdf? Selo = null);

// Ata/Anexo de Evidências da turma (item 5 da proposta do usuário, 04/09) — documento consolidado
// para auditoria: lista de presença biométrica com horários + as fotos da turma. Distinto do
// certificado individual (CertificadoTreinamentoPdfModelo), que é por trabalhador.
public record AtaSessaoTreinamentoPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    string CursoNome,
    string? NormaReferencia,
    DateTime DataRealizacao,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado,
    DateTime? DataEncerramento,
    IReadOnlyList<AtaSessaoTreinamentoPdfParticipanteModelo> Participantes,
    IReadOnlyList<byte[]> Fotos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    // 05/11: Certificado de Assinaturas Eletrônicas no fim da lista (modelo único, compacto).
    CertificadoAssinaturasModelo? Certificado = null,
    // 05/11: bloco de assinatura do instrutor no fim da lista. Nulo = sem instrutor identificado.
    AssinaturaInstrutorAtaPdf? Instrutor = null);

public record AssinaturaInstrutorAtaPdf(
    string Nome,
    // Ex.: "Técnico de Segurança do Trabalho · MTE-0123056/SP".
    string? CargoRegistro,
    DateTime? AssinadoEm,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura? Metodo,
    string? Hash);

public interface IAtaSessaoTreinamentoPdfService
{
    byte[] Gerar(AtaSessaoTreinamentoPdfModelo modelo);
}
