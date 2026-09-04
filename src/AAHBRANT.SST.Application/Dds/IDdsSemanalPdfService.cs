namespace AAHBRANT.SST.Application.Dds;

// Layout replica o documento em papel do usuário ("Registro Semanal de Diálogo Diário de Segurança
// - DDS", 31/08): cabeçalho com dados da semana, grade Seg-Sex com tema+data de cada dia, tabela
// única de presença (um trabalhador por linha, uma coluna de rubrica por dia) e assinaturas de
// encerramento no rodapé.
public record DdsSemanalPdfDiaModelo(DayOfWeek DiaSemana, DateTime Data, IReadOnlyList<string> AtividadesNomes, string? TemaLivreNome);

public record DdsSemanalPdfLinhaPresenca(
    string Nome,
    string Funcao,
    string Matricula,
    // 5 posições, segunda a sexta — true = trabalhador assinou/participou naquele dia.
    IReadOnlyList<bool> PresencaPorDia);

public record DdsSemanalPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string TipoLabel,
    string? EmpresaTerceirizada,
    string? NumeroDocumento,
    string? LocalFrenteServico,
    string ResponsavelNome,
    DateTime DataInicioSemana,
    DateTime DataFimSemana,
    IReadOnlyList<DdsSemanalPdfDiaModelo> Dias,
    IReadOnlyList<DdsSemanalPdfLinhaPresenca> Presencas,
    string? ResponsavelObraSstNome,
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);

public interface IDdsSemanalPdfService
{
    byte[] Gerar(DdsSemanalPdfModelo modelo);
}
