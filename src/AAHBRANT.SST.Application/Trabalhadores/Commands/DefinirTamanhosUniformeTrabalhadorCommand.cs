using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record ItemTamanhoUniforme(Guid CatalogoUniformeId, string Tamanho);

public record DefinirTamanhosUniformeTrabalhadorCommand(Guid TrabalhadorId, List<ItemTamanhoUniforme> Itens) : IRequest;

public class DefinirTamanhosUniformeTrabalhadorCommandValidator : AbstractValidator<DefinirTamanhosUniformeTrabalhadorCommand>
{
    public DefinirTamanhosUniformeTrabalhadorCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Itens).NotNull();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.CatalogoUniformeId).NotEmpty();
            item.RuleFor(i => i.Tamanho).NotEmpty().MaximumLength(20);
        });
    }
}

public class DefinirTamanhosUniformeTrabalhadorCommandHandler : IRequestHandler<DefinirTamanhosUniformeTrabalhadorCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _usuarioAtual;
    public DefinirTamanhosUniformeTrabalhadorCommandHandler(IAppDbContext db, ICurrentUserService usuarioAtual)
    {
        _db = db;
        _usuarioAtual = usuarioAtual;
    }

    public async Task Handle(DefinirTamanhosUniformeTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");
        ValidarObraAutorizada(trabalhador.ObraId);

        // Compatibilidade não destrutiva com o endpoint antigo: os itens enviados pelo cliente são
        // deliberadamente ignorados. Tamanho operacional só pode vir do G-RH, para que um payload
        // adulterado não altere o tamanho usado na entrega.
        await SincronizarTamanhosGrhAsync(trabalhador, ct);
    }

    public async Task SincronizarTamanhosGrhAsync(Trabalhador trabalhador, CancellationToken ct)
    {
        ValidarObraAutorizada(trabalhador.ObraId);
        var vinculosAtuais = await _db.TrabalhadorTamanhosUniforme.IgnoreQueryFilters()
            .Where(t => t.TrabalhadorId == trabalhador.Id)
            .ToListAsync(ct);

        var catalogos = await _db.CatalogoUniformes.ToListAsync(ct);
        var desejados = catalogos
            .Select(c => (Catalogo: c, Tamanho: TamanhoGrh(c, trabalhador)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Tamanho))
            .ToDictionary(x => x.Catalogo.Id, x => x.Tamanho!.Trim().ToUpperInvariant());

        foreach (var vinculo in vinculosAtuais)
        {
            if (desejados.TryGetValue(vinculo.CatalogoUniformeId, out var tamanho))
            {
                vinculo.Ativo = true;
                vinculo.Tamanho = tamanho;
            }
            else
            {
                vinculo.Ativo = false;
            }
        }

        var idsExistentes = vinculosAtuais.Select(v => v.CatalogoUniformeId).ToHashSet();
        foreach (var item in desejados.Where(i => !idsExistentes.Contains(i.Key)))
        {
            _db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme
            {
                TrabalhadorId = trabalhador.Id,
                CatalogoUniformeId = item.Key,
                Tamanho = item.Value,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string? TamanhoGrh(CatalogoUniforme catalogo, Trabalhador trabalhador)
    {
        // O catálogo não possui um tipo estruturado. A classificação usa Categoria primeiro e
        // Nome como fallback; quando ambos são desconhecidos, retornar null é intencionalmente
        // fail-closed: não inventa um tamanho e mantém eventual vínculo legado inativo.
        var categoria = Classificar(catalogo.Categoria);
        var nome = Classificar(catalogo.Nome);
        var tipo = categoria ?? nome;
        if (tipo == TipoTamanho.Calçado)
            return trabalhador.TamanhoCalcadoEpi;
        if (tipo == TipoTamanho.Calça)
            return trabalhador.TamanhoCalcaEpi;
        if (tipo == TipoTamanho.Blusa)
            return trabalhador.TamanhoBlusaEpi;
        return null;
    }

    private void ValidarObraAutorizada(Guid obraId)
    {
        if (!_usuarioAtual.TemAcessoGlobal && !_usuarioAtual.ObrasPermitidas.Contains(obraId))
            throw new UnauthorizedAccessException("O trabalhador não pertence a uma obra autorizada.");
    }

    private static TipoTamanho? Classificar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var tokens = Normalizar(valor).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(t => t is "calcado" or "bota" or "sapato" or "tenis" or "botina")) return TipoTamanho.Calçado;
        if (tokens.Any(t => t is "calca" or "bermuda")) return TipoTamanho.Calça;
        if (tokens.Any(t => t is "blusa" or "camisa" or "camiseta" or "polo")) return TipoTamanho.Blusa;
        return null;
    }

    private enum TipoTamanho { Blusa, Calça, Calçado }

    private static string Normalizar(string valor)
    {
        var decomposed = valor.Normalize(System.Text.NormalizationForm.FormD);
        return new string(decomposed
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
    }
}
