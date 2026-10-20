using System.Globalization;
using System.Text;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Riscos.Medidas;

// Decide, de forma determinística, o que entra validado: só a medida que a IA declarou como "PGR" e
// cujo trecho está de fato no texto original do PGR (comparação sem acento, caixa e pontuação). O
// resto — sugestão, ou "PGR" com trecho que não está no texto — entra Pendente, para a SST aceitar
// ou descartar.
public static class ConciliacaoMedidasPgr
{
    public record MedidaConciliada(FaseMedidaPrevencao Fase, string Texto, OrigemMedidaPrevencao Origem, StatusMedidaPrevencao Status);

    public static List<MedidaConciliada> Conciliar(string? textoPgr, IEnumerable<MedidaSugeridaIa> medidas)
    {
        var original = Normalizar(textoPgr);
        var resultado = new List<MedidaConciliada>();
        var vistos = new HashSet<string>();

        foreach (var m in medidas)
        {
            var texto = m.Texto?.Trim();
            if (string.IsNullOrWhiteSpace(texto)) continue;
            if (texto.Length > 1000) texto = texto[..1000];
            if (!vistos.Add(Normalizar(texto))) continue;

            var fase = Normalizar(m.Fase) switch
            {
                "antes" => FaseMedidaPrevencao.Antes,
                "apos" => FaseMedidaPrevencao.Apos,
                _ => FaseMedidaPrevencao.Durante,
            };

            var trecho = Normalizar(m.TrechoPgr);
            var doPgr = string.Equals(m.Origem, "PGR", StringComparison.OrdinalIgnoreCase)
                && trecho.Length >= 3 && original.Contains(trecho, StringComparison.Ordinal);

            resultado.Add(doPgr
                ? new MedidaConciliada(fase, texto, OrigemMedidaPrevencao.Pgr, StatusMedidaPrevencao.Validada)
                : new MedidaConciliada(fase, texto, OrigemMedidaPrevencao.SugestaoIa, StatusMedidaPrevencao.Pendente));
        }

        return resultado;
    }

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder();
        var espaco = false;
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); espaco = false; }
            else if (!espaco && sb.Length > 0) { sb.Append(' '); espaco = true; }
        }
        return sb.ToString().Trim();
    }
}
