namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

public enum FormatoAdesivo
{
    // Folha A4 com grade de 2 colunas x 4 linhas (8 posições); só a posição escolhida é impressa.
    A4 = 1,
    // Uma página só do tamanho do adesivo.
    Etiqueta = 2,
}

public record AdesivoInspecaoPdfModelo(
    SituacaoAdesivo Situacao,
    string ObjetoIdentificacao,
    string? ObjetoDescricao,
    string Tema,
    string ObraNome,
    // Logo cadastrada na Obra; nulo => o PDF usa a logo da AAHBRANT.
    byte[]? ObraLogoConteudo,
    DateTime InspecaoEm,
    // Só existe quando há periodicidade; nulo omite a linha "Próxima inspeção até".
    DateTime? ProximaInspecaoEm,
    string? InspetorNome,
    string? InspetorFuncao,
    string Protocolo,
    // URL completa do QR (/#/p/{uid}?i={protocolo}).
    string UrlQr,
    FormatoAdesivo Formato,
    // 1..8 (linha a linha: 1-2 primeira linha ... 7-8 última). Só vale no A4.
    int Posicao);

public interface IAdesivoInspecaoPdfService
{
    byte[] Gerar(AdesivoInspecaoPdfModelo modelo);
}
