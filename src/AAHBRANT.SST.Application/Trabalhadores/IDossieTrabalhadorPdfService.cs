namespace AAHBRANT.SST.Application.Trabalhadores;

// Uma linha do índice do dossiê: o documento e se ele entrou no PDF. Quando não entrou, Observacao diz
// por quê (fora do escopo de obra de quem baixou, arquivo em formato não suportado, falha ao gerar).
public record ItemIndiceDossie(string Secao, string Titulo, DateTime? Data, bool Incluido, string? Observacao = null);

// Peças do Dossiê do Funcionário que não existem em nenhum outro PDF: a página de índice e a página
// que embrulha um anexo em imagem (ASO, certificado ou termo digitalizado por foto) para entrar na junção.
public interface IDossieTrabalhadorPdfService
{
    byte[] GerarIndice(string trabalhadorNome, string obraNome, byte[]? obraLogo, IReadOnlyList<ItemIndiceDossie> itens);

    // Null quando os bytes não são uma imagem aceita.
    byte[]? ImagemComoPdf(string titulo, byte[] imagem, string obraNome, byte[]? obraLogo);
}
