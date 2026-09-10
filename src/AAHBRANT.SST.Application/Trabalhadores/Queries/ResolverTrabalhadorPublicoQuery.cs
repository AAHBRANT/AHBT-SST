using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// NTAG.md §1/§3.B.4 — resolução do "crachá digital" de um trabalhador. Diferente de
// ResolverAreaPublicaQuery (que também aceita o Código de negócio da área na URL), aqui só
// resolvemos pelo Uid opaco da tag: a matrícula do trabalhador costuma ser sequencial/previsível, e
// virar identificador permitiria "varrer" a URL. A rota da API também exige login: a tag só aponta
// para o perfil, quem decide se pode ver é a sessão/permissão/obra do usuário.
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

        // IgnoreQueryFilters() é necessário aqui: o filtro global Camada 3 (RBAC-Matrix.md §4) nega
        // acesso por padrão pra requisição anônima sem usuário logado (EscopoPorObraMiddleware,
        // TemAcessoGlobal=false + ObrasPermitidas=[] quando não há claim de identidade) — sem isso,
        // ESTA rota pública nunca resolve nenhum trabalhador em produção (só não aparece em dev, sem
        // Entra ID configurado). Reaplica t.Ativo manualmente: diferente de Obra/Função (linhas
        // abaixo), aqui o soft-delete continua valendo — funcionário desativado não deve manter
        // crachá público válido.
        var trabalhador = await _db.Trabalhadores
            .IgnoreQueryFilters()
            .Where(t => t.Id == trabalhadorId && t.Ativo)
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

        // Histórico de DDS (Diálogo Diário de Segurança) participado — pedido do usuário (09/09),
        // "tudo que for necessário do funcionário deve aparecer" no crachá. Só Data/Obra/Tema, nunca
        // a evidência de presença (foto/ScoreConfianca de DdsParticipante) — dado sensível demais
        // pra tela sem login. Sem paginação em EpisAtivos/Treinamentos porque essas listas são
        // naturalmente pequenas; DDS é diário e pode acumular anos de registros pra quem trabalha há
        // muito tempo, então limita às 20 participações mais recentes.
        //
        // IgnoreQueryFilters() necessário nas 2 consultas abaixo (mesmo motivo do trabalhador acima):
        // Dds tem o filtro RBAC de Camada 3 (nega tudo em requisição anônima); DdsParticipante tem só
        // Ativo (sem RBAC, não tem ObraId direto), mas mantém pelo mesmo cuidado. Ativo reaplicado
        // manualmente nas duas — DDS/participação cancelados (soft-delete) não devem aparecer.
        var ddsIdsParticipados = await _db.DdsParticipantes
            .IgnoreQueryFilters()
            .Where(p => p.TrabalhadorId == trabalhadorId && p.Ativo)
            .Select(p => p.DdsId)
            .ToListAsync(ct);

        var historicoDdsBruto = await _db.Dds
            .IgnoreQueryFilters()
            .Where(d => ddsIdsParticipados.Contains(d.Id) && d.Ativo)
            .OrderByDescending(d => d.Data)
            .Take(20)
            .Select(d => new { d.Data, d.ObraId, d.TemaLivreNome })
            .ToListAsync(ct);

        var obraIdsDds = historicoDdsBruto.Select(d => d.ObraId).Distinct().ToList();
        var nomesObrasDds = await _db.Obras.IgnoreQueryFilters()
            .Where(o => obraIdsDds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Nome, ct);

        var historicoDds = historicoDdsBruto
            .Select(d => new DdsPublicoDto(d.Data, nomesObrasDds.GetValueOrDefault(d.ObraId, string.Empty), d.TemaLivreNome))
            .ToList();

        return new TrabalhadorPublicoDto
        {
            TrabalhadorId = trabalhadorId,
            Nome = trabalhador.Nome,
            Matricula = trabalhador.Matricula ?? string.Empty,
            FuncaoNome = funcaoNome ?? string.Empty,
            ObraNome = obraNome ?? string.Empty,
            TemFoto = trabalhador.TemFoto,
            StatusAptidao = statusAptidao,
            EpisAtivos = episAtivos,
            Treinamentos = treinamentos,
            HistoricoDds = historicoDds,
        };
    }
}
