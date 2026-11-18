using System.Text.RegularExpressions;
using FluentValidation;

namespace AAHBRANT.SST.Application.Cameras;

// Regras compartilhadas do cadastro de equipamento de câmera IP.
public static class EquipamentoCameraRegras
{
    public const int TamanhoMaximoSenha = 100;

    // IP ou nome de rede (letras, números, ponto e hífen). Não aceita esquema (rtsp://), barra, '@' nem espaço: o agente
    // monta a URL com este valor e um host com "usuario@outro" ou "/caminho" desviaria a conexão para outro destino.
    private static readonly Regex HostValido = new(@"^[A-Za-z0-9]([A-Za-z0-9.\-]*[A-Za-z0-9])?$", RegexOptions.Compiled);

    public static bool HostEhValido(string? host) => !string.IsNullOrWhiteSpace(host) && host.Length <= 200 && HostValido.IsMatch(host);

    // Aceita AA:BB:CC:DD:EE:FF, AA-BB-..., AABBCCDDEEFF; guarda sempre em maiúsculas, com dois-pontos. Vazio = sem MAC.
    public static bool TentarNormalizarMac(string? informado, out string? normalizado)
    {
        normalizado = null;
        if (string.IsNullOrWhiteSpace(informado)) return true;

        var digitos = informado.Trim().Replace(":", "").Replace("-", "").ToUpperInvariant();
        if (digitos.Length != 12 || !digitos.All(Uri.IsHexDigit)) return false;

        normalizado = string.Join(':', Enumerable.Range(0, 6).Select(i => digitos.Substring(i * 2, 2)));
        return true;
    }

    public static string? LimparOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    public static IRuleBuilderOptions<T, string?> HostDeRede<T>(this IRuleBuilder<T, string?> regra)
        => regra.Must(HostEhValido).WithMessage("Informe o endereço da câmera só com o IP ou o nome na rede (sem rtsp://, barras ou espaços).");

    public static IRuleBuilderOptions<T, int> PortaDeRede<T>(this IRuleBuilder<T, int> regra, string nome)
        => regra.InclusiveBetween(1, 65535).WithMessage($"A porta {nome} precisa estar entre 1 e 65535.");

    public static IRuleBuilderOptions<T, string?> MacValido<T>(this IRuleBuilder<T, string?> regra)
        => regra.Must(m => TentarNormalizarMac(m, out _)).WithMessage("O endereço MAC precisa ter 12 dígitos hexadecimais (ex.: AA:BB:CC:DD:EE:FF).");
}
