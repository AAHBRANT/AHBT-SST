using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Inspecoes;

// Layout inspirado na planilha "Patrulha de Segurança do Trabalho" do usuário (31/08): um bloco por
// achado, com evidência anterior/posterior lado a lado — mesmo espírito do modelo original, mas no
// layout padrão de documentos do sistema (CabecalhoDocumentoPadrao, com a logo da obra).
public record InspecaoPdfItemModelo(
    int Ordem,
    string Descricao,
    string? Local,
    StatusItemChecklist? StatusItem,
    string? Observacao,
    string? PlanoDeAcao,
    string? ResponsavelNome,
    DateTime? Prazo,
    byte[]? FotoAntesConteudo,
    byte[]? FotoDepoisConteudo);

public record InspecaoPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string TipoInspecao,
    string ChecklistNome,
    int ChecklistVersao,
    DateTime Data,
    string ResponsavelNome,
    string Status,
    IReadOnlyList<InspecaoPdfItemModelo> Itens);

public interface IInspecaoPdfService
{
    byte[] Gerar(InspecaoPdfModelo modelo);
}
