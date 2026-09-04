namespace AAHBRANT.SST.Application.Dds;

public record DdsPdfTemaModelo(
    string AtividadeNome,
    string? PerigoNome,
    string? PerigoDescricao,
    string? Consequencia,
    string? ControlesExistentes,
    string? ControlesAdicionais);

public record DdsPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    DateTime Data,
    string ResponsavelNome,
    IReadOnlyList<DdsPdfTemaModelo> Temas,
    string? TemaLivreNome,
    string? TemaLivreDescricao,
    IReadOnlyList<(string Descricao, bool Verificado)> ItensChecklist,
    IReadOnlyList<string> ParticipantesNomes);

public interface IDdsPdfService
{
    byte[] Gerar(DdsPdfModelo modelo);
}
