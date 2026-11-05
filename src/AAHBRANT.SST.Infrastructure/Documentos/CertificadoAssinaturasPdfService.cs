using AAHBRANT.SST.Application.Assinatura;
using QuestPDF.Fluent;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class CertificadoAssinaturasPdfService : ICertificadoAssinaturasPdfService
{
    public byte[] Gerar(CertificadoAssinaturasModelo modelo) =>
        Document.Create(container => CertificadoAssinaturasPdf.Adicionar(container, modelo)).GeneratePdf();
}
