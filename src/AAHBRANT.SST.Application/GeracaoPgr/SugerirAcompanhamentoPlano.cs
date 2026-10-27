using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Sugere, para as ações do plano de ação que ainda não têm, a forma de acompanhamento, a periodicidade e o critério de
// aferição (NR-01, item 1.5.5.2). Só preenche campo vazio: o que o técnico já digitou nunca é sobrescrito, e o que vem da
// IA fica marcado até alguém conferir. Devolve quantas ações receberam sugestão.
public record SugerirAcompanhamentoPlanoGeracaoPgrCommand(Guid Id) : IRequest<int>;

public class SugerirAcompanhamentoPlanoGeracaoPgrCommandHandler : IRequestHandler<SugerirAcompanhamentoPlanoGeracaoPgrCommand, int>
{
    private const int MaxCaracteresCriterio = 300;

    private readonly IAppDbContext _db;
    private readonly IGeradorPgrIa _ia;

    public SugerirAcompanhamentoPlanoGeracaoPgrCommandHandler(IAppDbContext db, IGeradorPgrIa ia)
    {
        _db = db;
        _ia = ia;
    }

    public async Task<int> Handle(SugerirAcompanhamentoPlanoGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);

        var pendentes = e.AcoesPlano
            .Select((a, i) => (Acao: a, Indice: i))
            .Where(x => !string.IsNullOrWhiteSpace(x.Acao.Descricao)
                && (string.IsNullOrWhiteSpace(x.Acao.FormaAcompanhamento)
                    || string.IsNullOrWhiteSpace(x.Acao.PeriodicidadeAcompanhamento)
                    || string.IsNullOrWhiteSpace(x.Acao.CriterioAfericao)))
            .ToList();
        if (pendentes.Count == 0) return 0;

        var obra = await _db.Obras.Where(o => o.Id == g.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;
        var riscos = e.Ghes.SelectMany(gh => gh.Atividades).SelectMany(a => a.Riscos)
            .Where(r => !r.Excluido && !string.IsNullOrWhiteSpace(r.Perigo))
            .Select(r => r.Perigo.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(15).ToList();

        var sugestoes = await _ia.SugerirAcompanhamentoAsync(new ContextoAcompanhamentoPlano(
            obra, pendentes.Select(x => new AcaoParaAcompanhamento(x.Indice, x.Acao.Descricao.Trim(), x.Acao.Prazo)).ToList(), riscos), ct);

        var alteradas = 0;
        foreach (var s in sugestoes)
        {
            var alvo = pendentes.FirstOrDefault(x => x.Indice == s.Indice).Acao;
            if (alvo is null) continue;
            var mudou = false;
            if (string.IsNullOrWhiteSpace(alvo.FormaAcompanhamento) && AcompanhamentoPlano.Forma(s.Forma) is { } forma)
            {
                alvo.FormaAcompanhamento = forma;
                mudou = true;
            }
            if (string.IsNullOrWhiteSpace(alvo.PeriodicidadeAcompanhamento) && AcompanhamentoPlano.Periodicidade(s.Periodicidade) is { } periodo)
            {
                alvo.PeriodicidadeAcompanhamento = periodo;
                mudou = true;
            }
            var criterio = s.Criterio?.Trim();
            if (string.IsNullOrWhiteSpace(alvo.CriterioAfericao) && !string.IsNullOrEmpty(criterio))
            {
                alvo.CriterioAfericao = criterio.Length <= MaxCaracteresCriterio ? criterio : criterio[..(MaxCaracteresCriterio - 1)] + "…";
                mudou = true;
            }
            if (!mudou) continue;
            alvo.AcompanhamentoSugeridoPelaIa = true;
            alteradas++;
        }

        if (alteradas > 0)
        {
            g.EstruturaJson = SerializacaoGeracao.Gravar(e);
            await _db.SaveChangesAsync(ct);
        }
        return alteradas;
    }
}
