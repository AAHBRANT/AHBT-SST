using System.Security.Cryptography;
using System.Text;
using AAHBRANT.SST.Application.Assinatura.Queries;

namespace AAHBRANT.SST.Application.Assinatura;

// Compartilhado entre FinalizarDocumentoCommand (gera o hash) e VerificarIntegridadeQuery (recalcula
// para comparar) — precisa ser exatamente o mesmo algoritmo nos dois lugares, por isso vive num único
// lugar em vez de duplicado. SHA-256 puro, sem chave: aqui não é sigilo (o conteúdo em si — quem
// assinou, quando, por qual método — não é secreto), é só prova de integridade contra adulteração
// retroativa da lista de signatários. Fica em Application (não Infrastructure, ao contrário de
// IAuditoriaService/IPinHasher) porque SHA-256 é primitivo puro do BCL, sem chave de configuração
// nem estado — não viola a regra de dependência do Clean Architecture.
public static class HashConteudoDocumentoCalculador
{
    public static string Calcular(string entidadeTipo, Guid entidadeId, IEnumerable<DocumentoSignatarioDto> signatarios)
    {
        var partes = signatarios
            .OrderBy(s => s.AssinadoEm)
            .Select(s => $"{s.TrabalhadorId}|{(int)s.MetodoAutenticacao}|{s.AssinadoEm:O}");
        var conteudo = $"{entidadeTipo}|{entidadeId}|{string.Join(';', partes)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));
        return Convert.ToHexString(hash);
    }
}
