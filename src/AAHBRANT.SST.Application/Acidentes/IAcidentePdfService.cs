namespace AAHBRANT.SST.Application.Acidentes;

// Relatório da ocorrência (registro NBR 14280): identificação, envolvidos, descrição, lesão com o mapa
// corporal das partes atingidas, afastamento/gravidade, investigação, plano de ação e fotos do local.
// Cabeçalho e rodapé padrão (CabecalhoDocumentoPadrao/RodapeDocumentoPadrao), como os demais documentos.
public record AcidentePdfAcaoModelo(
    string Tipo,
    string Descricao,
    string Prioridade,
    DateTime? Prazo,
    string? ResponsavelNome,
    string Status);

public record AcidentePdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string Tipo,
    string Status,
    DateTime Data,
    TimeSpan? Hora,
    string Local,
    string? AtividadeNome,
    IReadOnlyList<string> Envolvidos,
    string Descricao,
    string? Lesao,
    // Códigos do mapa corporal (RegioesCorpoAcidente); o serviço desenha o mapa e lista os rótulos.
    IReadOnlyList<string> RegioesCorpo,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string Gravidade,
    int DiasDebitados,
    string? NumeroCat,
    string? Metodologia,
    string? Causas,
    IReadOnlyList<AcidentePdfAcaoModelo> Acoes,
    IReadOnlyList<byte[]> Fotos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura);

public interface IAcidentePdfService
{
    byte[] Gerar(AcidentePdfModelo modelo);
}
