namespace AAHBRANT.SST.Application.MateriaisApoio;

// Sem o campo Conteudo (bytes) de propósito — a listagem serve só de metadados; o conteúdo é
// buscado à parte por ObterConteudoMaterialApoioQuery quando o usuário efetivamente visualiza/baixa
// um item (mesmo raciocínio de InspecaoItemRespostaDto.TemFoto vs ObterFotoItemInspecaoQuery).
public class MaterialApoioDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanhoBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
