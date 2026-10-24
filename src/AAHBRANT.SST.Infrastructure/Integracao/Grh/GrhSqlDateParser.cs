using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// O banco do G-RH guarda datas como nvarchar (não date/datetime) e sem um formato único — visto na
// inspeção de schema de 2026-09-16: "criado_em" vem em ISO 8601 com hora (2026-09-15T17:15:36...) e
// "inicio"/"fim" vêm em data pura (2026-09-15), as duas dentro do mesmo par de tabelas
// (alojamentos/alojamento_moradores). Nunca confiar em um formato único — tenta o parse ISO
// completo primeiro (cobre os dois casos, DateTime.Parse aceita data pura e data+hora do mesmo
// jeito) e cai pra nulo (nunca lança) se vier algo inesperado, logando a chamador decidir o que fazer.
internal static class GrhSqlDateParser
{
    public static DateTime? Analisar(string? valorBruto)
    {
        if (string.IsNullOrWhiteSpace(valorBruto))
            return null;

        if (DateTime.TryParse(valorBruto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return data;

        // Fallback pra formatos explícitos comuns em base brasileira, caso o TryParse invariante falhe.
        string[] formatosConhecidos =
        [
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss",
        ];
        if (DateTime.TryParseExact(valorBruto, formatosConhecidos, CultureInfo.InvariantCulture, DateTimeStyles.None, out data))
            return data;

        return null;
    }

    // Lê uma coluna de data sem assumir o tipo físico da coluna — no banco do G-RH essas colunas
    // vêm como nvarchar hoje, mas se algum dia migrarem pra date/datetime2 de verdade, GetString()
    // lançaria InvalidCastException; GetValue() aceita os dois casos sem quebrar.
    public static DateTime? LerColuna(SqlDataReader leitor, int indice)
    {
        if (leitor.IsDBNull(indice))
            return null;

        var valor = leitor.GetValue(indice);
        return valor switch
        {
            DateTime dt => dt,
            string s => Analisar(s),
            _ => null,
        };
    }
}
