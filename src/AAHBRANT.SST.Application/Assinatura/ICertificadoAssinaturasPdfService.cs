namespace AAHBRANT.SST.Application.Assinatura;

// Certificado de Assinaturas Eletrônicas como PDF próprio (05/11): APR e PT saem sem ele — o documento fica
// limpo para ir à frente de serviço (sem biometria exposta) e o certificado é baixado à parte.
public interface ICertificadoAssinaturasPdfService
{
    byte[] Gerar(CertificadoAssinaturasModelo modelo);
}
