namespace AAHBRANT.SST.Application.Dds;

// Dados do resumo do DDS enviado ao Telegram quando o DDS é encerrado. Só matrícula, quantidades e obra:
// sem nome de funcionário, sem CPF e sem foto.
public record ResumoDdsDados(
    string Obra,
    DateTime Quando,
    int Presencas,
    int Total,
    int Facial,
    int Digital,
    int Falhas,
    IReadOnlyList<(string? Matricula, int Falhas)> Fracos)
{
    public int Pendentes => Math.Max(0, Total - Presencas);
}

// Gera a imagem (PNG) do relatório do DDS no padrão AAHBRANT. A implementação fica na Infrastructure
// porque depende do motor de PDF/imagem (QuestPDF).
public interface IImagemResumoDdsService
{
    byte[] Gerar(ResumoDdsDados dados);
}
