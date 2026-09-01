using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Inspecoes;

// Layout inspirado na planilha "Patrulha de Segurança do Trabalho" do usuário (31/08): um bloco por
// achado, com evidência anterior/posterior lado a lado. Cabeçalho próprio (não usa
// CabecalhoDocumentoPadrao — decisão do usuário, 01/09): slot de logo sempre em branco neste
// documento, ver InspecaoPdfService.CabecalhoInspecao.
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
