namespace AAHBRANT.SST.Application.Dds;

public record DdsPdfTemaModelo(
    string AtividadeNome,
    string? PerigoNome,
    string? PerigoDescricao,
    string? Consequencia,
    string? ControlesExistentes,
    string? ControlesAdicionais);

// AssinadoEm em UTC (padrão do banco); null quando a presença ainda não virou assinatura.
public record DdsPdfParticipanteModelo(
    string Nome,
    DateTime? AssinadoEm,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura? Metodo = null);

public record DdsPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    DateTime Data,
    string ResponsavelNome,
    IReadOnlyList<DdsPdfTemaModelo> Temas,
    string? TemaLivreNome,
    string? TemaLivreDescricao,
    IReadOnlyList<(string Descricao, bool Verificado)> ItensChecklist,
    IReadOnlyList<DdsPdfParticipanteModelo> Participantes,
    string? Protocolo,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura,
    // Assinatura do responsável/técnico (feita pelo botão "Assinar DDS" — Motor de Assinatura, ligada
    // ao trabalhador do usuário responsável). Nulos = ainda não assinou.
    DateTime? ResponsavelAssinadoEm = null,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura? ResponsavelMetodo = null,
    // Cargo/função do responsável (cadastro do trabalhador vinculado ao usuário), ex.: "Técnico de Segurança".
    string? ResponsavelFuncao = null);

public interface IDdsPdfService
{
    byte[] Gerar(DdsPdfModelo modelo);
}
