using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Numeração automática de documentos (pedido do usuário, 03/09): APR e PT não pedem mais o número
// no cadastro — o próprio sistema gera, no formato "{PREFIXO}-{ANO}-{SEQUENCIAL:D4}"
// (ex.: APR-2026-0001). Sequencial global (não por obra) que reinicia a cada ano; o ano usado é o
// da criação do documento, não a "Data" escolhida pelo usuário no formulário. IgnoreQueryFilters()
// no queryset passado pelo chamador é obrigatório: sem isso, o número de um registro excluído
// (soft-delete) poderia ser reaproveitado por um novo documento.
public static class GeradorNumeroDocumento
{
    public static async Task<string> GerarProximoAsync(
        IQueryable<string?> numerosExistentes,
        string prefixo,
        DateTime dataReferencia,
        CancellationToken ct)
    {
        var prefixoAno = $"{prefixo}-{dataReferencia.Year}-";

        var sufixos = await numerosExistentes
            .Where(n => n != null && n.StartsWith(prefixoAno))
            .Select(n => n!.Substring(prefixoAno.Length))
            .ToListAsync(ct);

        var maiorSequencial = sufixos
            .Select(s => int.TryParse(s, out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefixoAno}{(maiorSequencial + 1):D4}";
    }
}
