using System.Text.RegularExpressions;

namespace AAHBRANT.SST.AgenteCameras.Seguranca;

// Tira segredos de qualquer texto que vá para log: "password=..." e "usuario:senha@" de URLs RTSP, mais os
// valores literais registrados (segredo do agente, senhas das câmeras), inclusive na forma escapada de URL.
public sealed class Mascarador
{
    public const string Marca = "***";

    private static readonly Regex Senha = new(@"(?<chave>\b(?:password|passwd|pwd)=)[^&\s""']*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Credenciais = new(@"(?<esq>\brtsps?://[^/@\s:]+:)[^@\s]*(?<dir>@)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly object _trava = new();
    private string[] _literais = Array.Empty<string>();

    public void Registrar(string? segredo)
    {
        if (string.IsNullOrEmpty(segredo) || segredo.Length < 3) return;
        lock (_trava)
        {
            var lista = _literais.ToList();
            foreach (var v in new[] { segredo, Uri.EscapeDataString(segredo) })
                if (!lista.Contains(v)) lista.Add(v);
            // Os maiores primeiro: um segredo que contém outro não deixa sobra.
            _literais = lista.OrderByDescending(s => s.Length).ToArray();
        }
    }

    public string Mascarar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? "";
        var saida = Senha.Replace(texto, "${chave}" + Marca);
        saida = Credenciais.Replace(saida, "${esq}" + Marca + "${dir}");
        foreach (var literal in _literais)
            saida = saida.Replace(literal, Marca, StringComparison.Ordinal);
        return saida;
    }
}
