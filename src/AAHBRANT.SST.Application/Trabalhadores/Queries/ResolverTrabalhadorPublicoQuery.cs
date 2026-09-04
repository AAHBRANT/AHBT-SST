using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// NTAG.md §1/§3.B.4 — resolução pública do "crachá digital" de um trabalhador. Diferente de
// ResolverAreaPublicaQuery (que também aceita o Código de negócio da área na URL), aqui só
// resolvemos pelo Uid opaco da tag: a matrícula do trabalhador costuma ser sequencial/previsível, e
// virar identificador público permitiria "varrer" a URL e vazar o crachá de todo mundo. Por isso um
// trabalhador só fica acessível por esta rota depois de ter uma tag vinculada (TagsIdentificacaoTab).
public record ResolverTrabalhadorPublicoQuery(string Uid) : IRequest<TrabalhadorPublicoDto?>;

public class ResolverTrabalhadorPublicoQueryHandler : IRequestHandler<ResolverTrabalhadorPublicoQuery, TrabalhadorPublicoDto?>
{
    private readonly IAppDbContext _db;

    public ResolverTrabalhadorPublicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TrabalhadorPublicoDto?> Handle(ResolverTrabalhadorPublicoQuery request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == request.Uid, ct);
        if (tag is not { EntidadeVinculadaTipo: TipoEntidadeVinculada.Trabalhador, EntidadeVinculadaId: not null })
            return null;

        var trabalhadorId = tag.EntidadeVinculadaId.Value;

        var trabalhador = await _db.Trabalhadores
            .Where(t => t.Id == trabalhadorId)
            .Select(t => new { t.Nome, t.Matricula, t.ObraId, t.FuncaoId, TemFoto = t.FotoConteudo != null })
            .FirstOrDefaultAsync(ct);
        if (trabalhador is null) return null;

        // Obra/Função têm HasQueryFilter(Ativo) — mesmo cuidado de ObterPerfilCompletoTrabalhadorQuery:
        // o crachá precisa continuar resolvendo mesmo que a obra/função tenha sido desativada depois.
        var obraNome = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == trabalhador.ObraId)
            .Select(o => o.Nome)
            .FirstOrDefaultAsync(ct);
        var funcaoNome = await _db.Funcoes.IgnoreQueryFilters()
            .Where(f => f.Id == trabalhador.FuncaoId)
            .Select(f => f.Nome)
            .FirstOrDefaultAsync(ct);

        var resultadoAsoMaisRecente = await _db.Asos
            .Where(a => a.TrabalhadorId == trabalhadorId)
            .OrderByDescending(a => a.DataExame)
            .Select(a => (ResultadoAso?)a.ResultadoStatus)
            .FirstOrDefaultAsync(ct);
        var statusAptidao = resultadoAsoMaisRecente switch
        {
            null => "Sem ASO",
            ResultadoAso.Apto => "Apto",
            ResultadoAso.AptoComRestricao => "Apto com restrição",
            ResultadoAso.Inapto => "Inapto",
            _ => "Pendente",
        };

        var episAtivos = await _db.EntregasEpi
            .Where(e => e.TrabalhadorId == trabalhadorId && e.DataDevolucao == null)
            .Select(e => new EpiAtivoPublicoDto(e.CatalogoEpi!.Nome, e.DataValidade))
            .ToListAsync(ct);

        var treinamentos = await _db.Treinamentos
            .Where(t => t.TrabalhadorId == trabalhadorId)
            .OrderByDescending(t => t.DataValidade)
            .Select(t => new TreinamentoPublicoDto(t.CursoTreinamento!.Nome, t.DataValidade))
            .ToListAsync(ct);

        return new TrabalhadorPublicoDto
        {
            Nome = trabalhador.Nome,
            Matricula = trabalhador.Matricula,
            FuncaoNome = funcaoNome ?? string.Empty,
            ObraNome = obraNome ?? string.Empty,
            TemFoto = trabalhador.TemFoto,
            StatusAptidao = statusAptidao,
            EpisAtivos = episAtivos,
            Treinamentos = treinamentos,
        };
    }
}
