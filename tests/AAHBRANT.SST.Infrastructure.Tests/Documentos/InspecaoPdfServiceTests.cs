using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// Selo do técnico responsável no fim da inspeção (30/10): sai antes e depois da assinatura.
public class InspecaoPdfServiceTests
{
    public InspecaoPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static readonly byte[] QrFake = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static InspecaoPdfModelo Modelo(InspecaoPdfAssinaturaModelo? assinatura) => new(
        "Obra Beira Rio", null, "Andaimes", "Inspeção de Andaimes", 1, new DateTime(2026, 10, 30),
        "Gilton Pereira Miranda", assinatura is null ? "Em andamento" : "Concluída",
        new[]
        {
            new InspecaoPdfItemModelo(1, "Guarda-corpo e rodapé", null, null, StatusItemChecklist.Conforme, null, null, null, null, null, null),
            new InspecaoPdfItemModelo(2, "Ancoragem e travamento", null, null, StatusItemChecklist.Conforme, null, null, null, null, null, null),
        },
        "INS-2026-0142", "abc", "https://exemplo/validar", QrFake, assinatura is not null, assinatura);

    private static InspecaoPdfAssinaturaModelo Assinada() => new(
        new DateTime(2026, 10, 30, 17, 20, 0, DateTimeKind.Utc),
        "3f9a12c488b07d1e00112233445566778899aabbccddeeff0011223344556677",
        "https://exemplo/validar", QrFake,
        new[]
        {
            new InspecaoPdfSignatarioModelo("GILTON PEREIRA MIRANDA", "Digital", new DateTime(2026, 10, 30, 17, 20, 0, DateTimeKind.Utc),
                "Técnico de Segurança do Trabalho", MetodoAutenticacaoAssinatura.Biometria),
        });

    [Fact]
    public void Gerar_SemAssinatura_E_Assinada_GeramPdf()
    {
        var servico = new InspecaoPdfService();
        var pendente = servico.Gerar(Modelo(null));
        var assinada = servico.Gerar(Modelo(Assinada()));

        Assert.True(pendente.Length > 1000);
        Assert.True(assinada.Length > pendente.Length);

        var saida = Environment.GetEnvironmentVariable("SST_PDF_SAIDA");
        if (!string.IsNullOrEmpty(saida))
        {
            File.WriteAllBytes(Path.Combine(saida, "inspecao-pendente.pdf"), pendente);
            File.WriteAllBytes(Path.Combine(saida, "inspecao-assinada.pdf"), assinada);
        }
    }
}
