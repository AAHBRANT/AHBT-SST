using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Inspecoes;

// Layout inspirado na planilha "Patrulha de Segurança do Trabalho" do usuário (31/08): um bloco por
// achado, com evidência anterior/posterior lado a lado. Cabeçalho: CabecalhoDocumentoPadrao, mesmo
// componente usado por APR/PT/DDS/CIPA/etc. — a exceção que mantinha o slot de logo em branco só
// para este documento (decisão de 01/09) foi revertida a pedido do usuário em 2026-09-09, pra
// manter o visual padronizado entre todos os documentos gerados pelo sistema.
public record InspecaoPdfItemModelo(
    int Ordem,
    string Descricao,
    string? Secao,
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
    string TipoInspecao,
    string ChecklistNome,
    int ChecklistVersao,
    DateTime Data,
    string ResponsavelNome,
    string Status,
    IReadOnlyList<InspecaoPdfItemModelo> Itens,
    byte[]? ObraLogoConteudo = null);

public interface IInspecaoPdfService
{
    byte[] Gerar(InspecaoPdfModelo modelo);
}
