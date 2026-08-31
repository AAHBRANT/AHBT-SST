namespace AAHBRANT.SST.Application.Dds;

public record DdsPdfModelo(
    string ObraNome,
    byte[]? ObraLogoConteudo,
    DateTime Data,
    string ResponsavelNome,
    string TopicoPrincipal,
    IReadOnlyList<string> AtividadesNomes,
    IReadOnlyList<(string Descricao, bool Verificado)> ItensChecklist,
    IReadOnlyList<string> ParticipantesNomes);

public interface IDdsPdfService
{
    byte[] Gerar(DdsPdfModelo modelo);
}
