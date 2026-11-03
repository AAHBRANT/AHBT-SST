using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Ações de melhoria no plano de ação do PGR (comentário do engenheiro sobre o Beira Rio, 03/11/2026): o plano não pode
// ter só o cronograma de treinamentos, precisa das ações que reduzem os riscos. Sai das medidas dos riscos de nível alto
// ou acima (Eliminação, EPC e Administrativa; EPI é entrega de rotina e fica de fora; sem categoria, de rascunho antigo, entra por último). Sem IA: é a própria medida do
// risco. A ação nasce sem responsável e sem prazo (o PGR segue com a pendência até alguém preencher) e nunca duplica
// uma ação que já está no plano. Devolve quantas ações entraram.
public record SugerirAcoesMelhoriaPlanoGeracaoPgrCommand(Guid Id) : IRequest<int>;

public class SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler : IRequestHandler<SugerirAcoesMelhoriaPlanoGeracaoPgrCommand, int>
{
    private const int MaxAcoes = 20;
    private const int MaxPorPerigo = 2;
    private const int MaxCaracteres = 500;

    private readonly IAppDbContext _db;

    public SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(SugerirAcoesMelhoriaPlanoGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);

        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        if (matriz is null) return 0;

        var novas = Montar(e, matriz.Celulas.Select(c => (c.Probabilidade, c.Severidade, c.NivelRisco)).ToList());
        if (novas.Count == 0) return 0;

        e.AcoesPlano.AddRange(novas);
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        await _db.SaveChangesAsync(ct);
        return novas.Count;
    }

    // Separado do handler para testar sem banco. Não altera a estrutura; só devolve o que falta no plano.
    public static List<AcaoPlanoGerada> Montar(EstruturaGeracaoPgr e, IReadOnlyList<(int P, int S, NivelRisco Nivel)> matriz)
    {
        var existentes = e.AcoesPlano
            .Where(a => !string.IsNullOrWhiteSpace(a.Descricao))
            .Select(a => FuncaoSstClassifier.Normalizar(a.Descricao)).ToHashSet();

        var candidatas = new List<(int Prioridade, int Peso, string Perigo, string Texto)>();
        foreach (var r in e.Ghes.SelectMany(gh => gh.Atividades).SelectMany(a => a.Riscos))
        {
            if (r.Excluido || string.IsNullOrWhiteSpace(r.Perigo)) continue;
            var nivel = matriz.FirstOrDefault(c => c.P == r.Probabilidade && c.S == r.Severidade).Nivel;
            if (nivel < NivelRisco.Alto) continue;
            foreach (var m in r.Medidas.Where(m => m.EntraNoPgr() && !string.IsNullOrWhiteSpace(m.Texto)))
            {
                var prioridade = m.Categoria switch
                {
                    CategoriasMedida.Eliminacao => 0,
                    CategoriasMedida.Epc => 1,
                    CategoriasMedida.Administrativa => 2,
                    CategoriasMedida.Epi => -1, // EPI é entrega de rotina, não ação de melhoria.
                    _ => 3, // Rascunho antigo, sem categoria: entra por último; o engenheiro confere.
                };
                if (prioridade < 0) continue;
                candidatas.Add((prioridade, -(r.Probabilidade * r.Severidade), r.Perigo.Trim(), m.Texto.Trim()));
            }
        }

        // No máximo duas por perigo (as de maior prioridade): sem isso um risco com cinco medidas ocupa o plano inteiro.
        candidatas = candidatas.GroupBy(x => FuncaoSstClassifier.Normalizar(x.Perigo))
            .SelectMany(g => g.OrderBy(x => x.Prioridade).Take(MaxPorPerigo)).ToList();

        var novas = new List<AcaoPlanoGerada>();
        foreach (var c in candidatas.OrderBy(x => x.Prioridade).ThenBy(x => x.Peso))
        {
            if (novas.Count >= MaxAcoes) break;
            var descricao = Limitar($"{c.Texto} (risco: {c.Perigo})");
            // A mesma medida aparece em várias atividades e GHEs: entra uma vez só.
            if (!existentes.Add(FuncaoSstClassifier.Normalizar(descricao))) continue;
            novas.Add(new AcaoPlanoGerada { Descricao = descricao });
        }
        return novas;
    }

    private static string Limitar(string s) => s.Length <= MaxCaracteres ? s : s[..(MaxCaracteres - 1)] + "…";
}
