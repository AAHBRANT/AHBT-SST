namespace AAHBRANT.SST.Application.Acidentes;

// Relatório de Investigação de Ocorrência (pedido de 23/10, desenho aprovado em
// docs/mockups/relatorio-investigacao-ocorrencia-v1.html): tudo o que já está no registro da
// ocorrência — identificação, descrição, consequências, análise/causas, plano de ação, reunião e
// fotos — num PDF com o cabeçalho/rodapé padrão. Os textos já chegam traduzidos (rótulos dos enums),
// o serviço de PDF só desenha.
public record InvestigacaoOcorrenciaPdfAcaoModelo(
    string Descricao,
    string? Fundamentacao,
    string Tipo,
    string? ResponsavelNome,
    DateTime? Prazo,
    string Situacao,
    bool Concluida);

public record InvestigacaoOcorrenciaPdfFotoModelo(
    byte[] Conteudo,
    DateTime? CapturadaEmUtc,
    double? Latitude,
    double? Longitude);

public record InvestigacaoOcorrenciaPdfReuniaoModelo(
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<string> Participantes);

public record InvestigacaoOcorrenciaPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string Tipo,
    DateTime Data,
    TimeSpan? Hora,
    string Local,
    string? AtividadeNome,
    IReadOnlyList<string> Envolvidos,
    string Situacao,
    DateTime? DataConclusaoInvestigacao,
    string Descricao,
    string Gravidade,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    int DiasDebitados,
    string? Lesao,
    string? Atendimento,
    string? Consequencia,
    string? Metodologia,
    string? Causas,
    IReadOnlyList<InvestigacaoOcorrenciaPdfAcaoModelo> Acoes,
    InvestigacaoOcorrenciaPdfReuniaoModelo? Reuniao,
    IReadOnlyList<InvestigacaoOcorrenciaPdfFotoModelo> Fotos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);

public interface IInvestigacaoOcorrenciaPdfService
{
    byte[] Gerar(InvestigacaoOcorrenciaPdfModelo modelo);
}
