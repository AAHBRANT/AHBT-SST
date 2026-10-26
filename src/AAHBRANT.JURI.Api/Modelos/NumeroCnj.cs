namespace AAHBRANT.JURI.Api.Modelos;

// Número único CNJ (Res. CNJ 65/2008): NNNNNNN-DD.AAAA.J.TR.OOOO → 20 dígitos.
// J = segmento da Justiça (1 STF, 3 STJ, 4 Federal, 5 Trabalho, 6 Eleitoral, 8 Estadual);
// TR = tribunal dentro do segmento. Daí derivamos a sigla e o alias usado pela API DataJud
// (api_publica_{alias}), porque o DataJud exige um índice por tribunal e não aceita busca nacional.
public static class NumeroCnj
{
    private static readonly Dictionary<string, string> UfPorCodigo = new()
    {
        ["01"] = "AC", ["02"] = "AL", ["03"] = "AP", ["04"] = "AM", ["05"] = "BA", ["06"] = "CE", ["07"] = "DF",
        ["08"] = "ES", ["09"] = "GO", ["10"] = "MA", ["11"] = "MT", ["12"] = "MS", ["13"] = "MG", ["14"] = "PA",
        ["15"] = "PB", ["16"] = "PR", ["17"] = "PE", ["18"] = "PI", ["19"] = "RJ", ["20"] = "RN", ["21"] = "RS",
        ["22"] = "RO", ["23"] = "RR", ["24"] = "SC", ["25"] = "SE", ["26"] = "SP", ["27"] = "TO",
    };

    public static string Limpar(string numero) => new(numero.Where(char.IsDigit).ToArray());

    public static bool EhValido(string numero) => Limpar(numero).Length == 20;

    public static string Formatar(string numero)
    {
        var n = Limpar(numero);
        if (n.Length != 20) return numero;
        return $"{n[..7]}-{n[7..9]}.{n[9..13]}.{n[13]}.{n[14..16]}.{n[16..]}";
    }

    public static string Justica(string numero)
    {
        var n = Limpar(numero);
        if (n.Length != 20) return "desconhecida";
        return n[13] switch
        {
            '1' => "stf",
            '2' => "cnj",
            '3' => "stj",
            '4' => "federal",
            '5' => "trabalhista",
            '6' => "eleitoral",
            '7' => "militar_uniao",
            '8' => "estadual",
            '9' => "militar_estadual",
            _ => "desconhecida",
        };
    }

    // Retorna (sigla do tribunal, alias DataJud) ou null quando o segmento não é suportado.
    public static (string Sigla, string Alias)? ResolverTribunal(string numero)
    {
        var n = Limpar(numero);
        if (n.Length != 20) return null;
        var j = n[13];
        var tr = n[14..16];
        var trNum = int.Parse(tr);

        switch (j)
        {
            case '1': return ("STF", "stf");
            case '3': return ("STJ", "stj");
            case '4': return ($"TRF{trNum}", $"trf{trNum}");
            case '5': return trNum == 0 ? ("TST", "tst") : ($"TRT{trNum}", $"trt{trNum}");
            case '6':
                if (!UfPorCodigo.TryGetValue(tr, out var ufE)) return null;
                return ($"TRE-{ufE}", $"tre-{ufE.ToLowerInvariant()}");
            case '8':
                if (!UfPorCodigo.TryGetValue(tr, out var uf)) return null;
                return uf == "DF" ? ("TJDFT", "tjdft") : ($"TJ{uf}", $"tj{uf.ToLowerInvariant()}");
            default: return null;
        }
    }
}
