using System.Globalization;
using System.Text;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Funcoes;

// Sugestão da cor do capacete pelo NOME da função, no padrão AAHBRANT (decidido pelo usuário em 14/11/2026).
// Só sugere: quem decide é o usuário, na conferência da tela de Funções. Nome que não se encaixa devolve SemPadrao.
// A comparação ignora acento, maiúsculas e pontuação, e vale pelo INÍCIO de cada palavra ("engenheir" pega
// engenheiro/engenheira). Vale a primeira regra que casar, nesta ordem (a ordem resolve os empates):
//   1. Vermelho (brigadista, bombeiro civil) antes de tudo; "bombeiro hidráulico" não casa com "bombeiro civil".
//   2. Laranja (visitante, temporário).
//   3. Branco (engenheiro, arquiteto, mestre, encarregado, gestor...) antes do verde: "Engenheiro de Segurança do Trabalho" é branco.
//   4. Verde (técnico de segurança do trabalho, meio ambiente).
//   5. Amarelo de ajudante/servente antes do azul: "Ajudante de Eletricista" é execução, não eletricista.
//   6. Azul (eletricista, encanador, operador de máquinas, técnico especializado).
//   7. Amarelo (operário, pedreiro, carpinteiro, armador, pintor e demais ofícios de execução).
public static class SugestaoCorCapacete
{
    private static readonly (CorCapacete Cor, string[] Palavras)[] Regras =
    {
        (CorCapacete.Vermelho, new[] { "brigad", "bombeiro civil", "emergencia" }),
        (CorCapacete.Laranja, new[] { "visitante", "temporari" }),
        (CorCapacete.Branco, new[] { "engenheir", "arquitet", "mestre", "encarregad", "gestor", "gerente", "coordenador", "supervisor" }),
        (CorCapacete.Verde, new[]
        {
            "tecnico de seguranca", "tecnico em seguranca", "tecnico seguranca", "tec de seguranca", "tec seguranca", "tst",
            "seguranca do trabalho", "meio ambiente", "ambiental",
        }),
        (CorCapacete.Amarelo, new[] { "ajudante", "servente" }),
        (CorCapacete.Azul, new[] { "eletricista", "encanador", "bombeiro hidraulico", "operador", "tecnico" }),
        (CorCapacete.Amarelo, new[]
        {
            "operario", "pedreiro", "carpinteiro", "armador", "pintor", "gesseiro", "azulejista", "ladrilhista",
            "soldador", "serralheiro", "marceneiro", "montador", "impermeabilizador", "vidraceiro",
        }),
    };

    // Texto curto do grupo do padrão AAHBRANT de cada cor, para a conferência na tela.
    public static string? Grupo(CorCapacete cor) => cor switch
    {
        CorCapacete.Branco => "Engenheiros, mestres, encarregados, gestores",
        CorCapacete.Amarelo => "Operários, serventes, ajudantes, execução",
        CorCapacete.Azul => "Eletricistas, encanadores, operadores, técnicos especializados",
        CorCapacete.Verde => "Técnico de segurança, meio ambiente",
        CorCapacete.Vermelho => "Brigadistas e emergência",
        CorCapacete.Laranja => "Visitantes e temporários",
        _ => null,
    };

    public static CorCapacete Sugerir(string? nomeDaFuncao)
    {
        var nome = Normalizar(nomeDaFuncao);
        if (nome.Length == 0) return CorCapacete.SemPadrao;

        foreach (var (cor, palavras) in Regras)
            foreach (var palavra in palavras)
                if (nome.Contains(" " + palavra, StringComparison.Ordinal)) return cor;
        return CorCapacete.SemPadrao;
    }

    // Minúsculas, sem acento, só letras e dígitos separados por um espaço, com espaço antes e depois.
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder(" ");
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb[^1] != ' ') sb.Append(' ');
        }
        if (sb[^1] != ' ') sb.Append(' ');
        return sb.ToString();
    }
}
