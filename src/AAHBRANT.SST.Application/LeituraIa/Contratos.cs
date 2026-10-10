using AAHBRANT.SST.Application.Ghes.Commands;

namespace AAHBRANT.SST.Application.LeituraIa;

// Texto do PDF, uma string por página (implementado em Infrastructure com PdfPig).
public interface IExtratorTextoPdf
{
    IReadOnlyList<string> ExtrairPaginas(byte[] pdf);
}

// Leitura do PGR/PCMSO pela IA, em partes (uma chamada por GHE / por quadro de exames). Implementado em
// Infrastructure sobre o Azure OpenAI. "progresso" é chamado em sequência (nunca em paralelo).
public interface ILeitorDocumentoSstIa
{
    Task<LeituraPgrIa> LerPgrAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct);
    Task<LeituraPcmsoIa> LerPcmsoAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct);
}

// Fila em memória do processamento em segundo plano (BackgroundService na Api).
public interface IFilaLeituraIa
{
    ValueTask EnfileirarAsync(Guid leituraId, CancellationToken ct = default);
}

// ---- O que a IA devolve ----

public record CabecalhoPgrIa(string? Responsavel, string? Registro, string? Revisao, DateTime? Inicio, DateTime? RevisaoSugerida, DateTime? Termino);

public record LeituraPgrIa(CabecalhoPgrIa Cabecalho, List<ImportarEstruturaGhe> Ghes, List<string> PlanoAcao);

public record CabecalhoPcmsoIa(string? MedicoNome, string? MedicoCrm, DateTime? DataElaboracao, DateTime? Validade);

public record ExameQuadroIa(string Exame, string? Codigo, bool Admissional, bool Periodico, bool RetornoTrabalho, bool MudancaRisco, bool Demissional);

// A coluna de periodicidade do quadro pode vir desalinhada do PDF (caso real do PCMSO do Parque Roger):
// a IA devolve os exames e as periodicidades cada um na ordem em que aparecem, e o casamento por ordem
// é feito aqui, de forma determinística (ver MontagemLeituraIa).
// PeriodicidadesNoTexto: os valores da coluna tirados do texto do PDF sem IA (MontagemLeituraIa.
// PeriodicidadesNoTexto) — na leitura real de 10/10/2026 a IA errou a contagem em 15 dos 27 quadros e o
// texto acertou todos; a da IA fica como reserva.
public record QuadroPcmsoIa(string Funcao, string? Ghe, string? Cbo, List<ExameQuadroIa> Exames, List<string> PeriodicidadesNaOrdem,
    List<string>? PeriodicidadesNoTexto = null);

public record LeituraPcmsoIa(CabecalhoPcmsoIa Cabecalho, List<QuadroPcmsoIa> Quadros);

// ---- O que fica guardado para a tela de revisão (LeituraDocumentoIa.ResultadoJson) ----

public record FuncaoLidaIa(
    string NomeDocumento,
    string? Cbo,
    List<int> Ghes,
    int Exames,
    Guid? FuncaoIdSugerida,
    string? FuncaoNomeSugerida,
    // "existe" (mesmo nome), "parecida" (sugestão a confirmar) ou "nova" (será criada)
    string Situacao);

// Gravidade: "alta" ou "media".
public record DivergenciaLeituraIa(string Gravidade, string Texto);

// Tipo: "incluido", "removido" ou "alterado".
public record DiferencaLeituraIa(string Tipo, string Oque, string? Antes, string? Depois);

public record ResultadoLeituraIa(
    string Documento,
    CabecalhoPgrIa? CabecalhoPgr,
    CabecalhoPcmsoIa? CabecalhoPcmso,
    List<ImportarEstruturaGhe> Ghes,
    List<string> PlanoAcao,
    List<ImportarEstruturaExamesFuncao> ExamesPorFuncao,
    List<FuncaoLidaIa> Funcoes,
    List<DivergenciaLeituraIa> Divergencias,
    // Vazio na primeira leitura da obra (não há estrutura anterior para comparar).
    List<DiferencaLeituraIa> Diferencas,
    bool HaEstruturaAtual);
