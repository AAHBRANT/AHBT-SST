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

    public Domain.Enums.TipoDocumentoSst Tipo { get; set; }
    public string? Codigo { get; set; }
    public string? Revisao { get; set; }
    public Domain.Enums.SituacaoDocumentoSst? Situacao { get; set; }
    public string? FormaConsulta { get; set; }
    public bool IncluirEmTodasOs { get; set; }
    // Quantas atividades usam o documento (só procedimento/IT).
    public int Atividades { get; set; }
}
