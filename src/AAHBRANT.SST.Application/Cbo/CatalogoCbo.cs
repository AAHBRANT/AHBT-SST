using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cbo;

// Código CBO: guardado com 6 dígitos ("717020") e exibido como "7170-20". Aceita o que vier do cadastro antigo
// ("7170-20", "717020", "717010 - Operador de martelete") e devolve nulo quando não há 6 dígitos no início.
public static class CodigoCbo
{
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var digitos = new string(valor.TakeWhile(c => !char.IsLetter(c)).Where(char.IsDigit).ToArray());
        return digitos.Length == 6 ? digitos : null;
    }

    public static string Formatar(string codigo6) => codigo6.Length == 6 ? $"{codigo6[..4]}-{codigo6[4..]}" : codigo6;
}

public record ResultadoBuscaCbo(string Codigo, string CodigoFormatado, string Titulo, string? Sinonimo);

// Tabela oficial da CBO 2002 em memória (cerca de 10 mil títulos entre oficiais e sinônimos), para busca e validação.
public class CatalogoCbo
{
    private sealed record Linha(string Codigo, string Titulo, bool Oficial, string Normalizado, string[] Palavras);

    private static readonly HashSet<string> PalavrasVazias = new() { "de", "da", "do", "das", "dos", "e", "em", "a", "o", "para", "com" };

    private readonly List<Linha> _linhas;
    private readonly Dictionary<string, string> _oficiais;

    public CatalogoCbo(IEnumerable<(string Codigo, string Titulo, bool Oficial)> titulos)
    {
        _linhas = titulos.Select(t =>
        {
            var n = FuncaoSstClassifier.Normalizar(t.Titulo);
            return new Linha(t.Codigo, t.Titulo, t.Oficial, n, Palavras(n));
        }).ToList();
        _oficiais = new();
        foreach (var l in _linhas.Where(l => l.Oficial)) _oficiais.TryAdd(l.Codigo, l.Titulo);
    }

    public static async Task<CatalogoCbo> CarregarAsync(IAppDbContext db, CancellationToken ct) =>
        new((await db.TitulosCbo.Select(t => new { t.Codigo, t.Titulo, t.Oficial }).ToListAsync(ct))
            .Select(t => (t.Codigo, t.Titulo, t.Oficial)));

    public bool Vazio => _oficiais.Count == 0;

    public bool Existe(string? codigo) => CodigoCbo.Normalizar(codigo) is { } c && _oficiais.ContainsKey(c);

    public string? TituloOficial(string? codigo) => CodigoCbo.Normalizar(codigo) is { } c ? _oficiais.GetValueOrDefault(c) : null;

    // Busca da tela: por código (começo dos dígitos) ou por palavras do título e dos sinônimos. Um resultado por código.
    public List<ResultadoBuscaCbo> Buscar(string? texto, int maximo = 20)
    {
        var termo = FuncaoSstClassifier.Normalizar(texto ?? string.Empty);
        if (termo.Length < 2) return new();
        var digitos = new string(termo.Where(char.IsDigit).ToArray());
        if (digitos.Length >= 2 && termo.All(c => char.IsDigit(c) || c is '-' or '.' or ' '))
            return _linhas.Where(l => l.Oficial && l.Codigo.StartsWith(digitos)).OrderBy(l => l.Codigo).Take(maximo).Select(Resultado).ToList();

        var palavras = Palavras(termo);
        if (palavras.Length == 0) return new();
        return _linhas
            .Where(l => palavras.All(p => l.Palavras.Any(w => w.StartsWith(p))))
            .Select(l => (L: l, Nota: l.Normalizado == termo ? 0 : l.Normalizado.StartsWith(termo) ? 1 : 2))
            .OrderBy(x => x.Nota).ThenBy(x => x.L.Oficial ? 0 : 1).ThenBy(x => x.L.Titulo.Length)
            .GroupBy(x => x.L.Codigo).Select(g => g.First().L)
            .Take(maximo)
            .Select(Resultado)
            .ToList();
    }

    // Códigos candidatos para o nome de uma função, para a IA escolher: os que mais compartilham palavras com o nome.
    public List<ResultadoBuscaCbo> Candidatos(string nomeFuncao, int maximo = 12)
    {
        var palavras = Palavras(FuncaoSstClassifier.Normalizar(nomeFuncao));
        if (palavras.Length == 0) return new();
        return _linhas
            .Select(l => (L: l, Nota: palavras.Count(p => l.Palavras.Any(w => w.StartsWith(p) || p.StartsWith(w) && w.Length >= 5))))
            .Where(x => x.Nota > 0)
            .OrderByDescending(x => x.Nota).ThenBy(x => x.L.Palavras.Length).ThenBy(x => x.L.Oficial ? 0 : 1)
            .GroupBy(x => x.L.Codigo).Select(g => g.First().L)
            .Take(maximo)
            .Select(Resultado)
            .ToList();
    }

    private ResultadoBuscaCbo Resultado(Linha l) =>
        new(l.Codigo, CodigoCbo.Formatar(l.Codigo), _oficiais.GetValueOrDefault(l.Codigo) ?? l.Titulo, l.Oficial ? null : l.Titulo);

    private static string[] Palavras(string normalizado) =>
        normalizado.Split(new[] { ' ', '/', '-', '(', ')', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 2 && !PalavrasVazias.Contains(p)).ToArray();
}

public record BuscarCboQuery(string? Busca) : IRequest<List<ResultadoBuscaCbo>>;

public class BuscarCboQueryHandler : IRequestHandler<BuscarCboQuery, List<ResultadoBuscaCbo>>
{
    private readonly IAppDbContext _db;

    public BuscarCboQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ResultadoBuscaCbo>> Handle(BuscarCboQuery request, CancellationToken ct) =>
        (await CatalogoCbo.CarregarAsync(_db, ct)).Buscar(request.Busca);
}
