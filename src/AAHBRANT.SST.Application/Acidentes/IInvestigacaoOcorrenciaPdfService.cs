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

// Reunião realizada: sai data, presentes e ata. Só agendada: sai o horário e os convidados.
public record InvestigacaoOcorrenciaPdfReuniaoModelo(
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<string> Participantes,
    bool NoTeams,
    DateTime? RealizadaEm = null,
    IReadOnlyList<string>? Presentes = null,
    string? Ata = null);

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
    bool TemAssinatura,
    // Códigos do mapa corporal (RegioesCorpoAcidente): o serviço desenha o mapa e lista os rótulos.
    IReadOnlyList<string>? RegioesCorpo = null);

public interface IInvestigacaoOcorrenciaPdfService
{
    byte[] Gerar(InvestigacaoOcorrenciaPdfModelo modelo);
}
