namespace AAHBRANT.SST.Application.CatalogosUniforme;

// Chaves estáveis; nenhuma associação por cor é inferida a partir do nome genérico da peça.
public static class ReferenciasUniforme
{
    public static readonly string[] Chaves = [
        "camisa-brim-laranja", "camisa-brim-azul", "camisa-brim-marsala",
        "camisa-feminina-marsala", "camisa-feminina-cinza", "calca-brim-laranja",
        "calca-brim-azul", "calca-brim-marsala", "calca-brim-cinza", "agasalho-laranja"
    ];
    public static bool Valida(string? chave) => chave is null || Chaves.Contains(chave);

    public static async Task<byte[]> LerFotoAsync(string chave, CancellationToken ct)
    {
        if (!Chaves.Contains(chave)) throw new ArgumentException("Modelo visual inválido.", nameof(chave));
        await using var stream = typeof(ReferenciasUniforme).Assembly.GetManifestResourceStream(
            $"AAHBRANT.SST.Application.CatalogosUniforme.Referencias.{chave}.jpeg")
            ?? throw new InvalidOperationException("Ficha de uniforme não encontrada.");
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, ct);
        return output.ToArray();
    }
}

public static class NomeUniforme
{
    public static string Padronizar(string nome)
    {
        var padrao = string.Join(" ", nome.Trim().ToUpperInvariant().Replace("BROM", "BRIM").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return padrao == "CALCA BRIM" ? "CALÇA BRIM" : padrao;
    }
}
