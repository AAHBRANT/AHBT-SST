using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EscopoPcmso;

// Comparativo de funções entre o PGR vigente e o PCMSO da obra (apontamento da supervisão, 03/08/2026: funções
// diferentes entre os dois documentos). O PCMSO entra pelas funções com exames definidos (ExameFuncaoObra); o texto
// livre "funções contempladas" não conta. PGR e PCMSO usam o mesmo cadastro de funções, então o nome só diverge
// quando o cadastro tem duas funções diferentes.
public enum SituacaoFuncaoPgrPcmso
{
    EmOrdem = 1,
    FaltaNoPcmso = 2,
    FaltaNoPgr = 3,
    FaltaNosDois = 4,
}

public record ComparativoFuncaoDto(
    Guid FuncaoId, string Funcao, List<int> GhesPgr, int ExamesPcmso, int Trabalhadores, SituacaoFuncaoPgrPcmso Situacao);

public record ComparativoPgrPcmsoDto(
    Guid ObraId,
    string ObraNome,
    string? PgrNome,
    // Nulo quando dá para criar o rascunho do PCMSO; senão o motivo, para a tela explicar.
    string? MotivoSemRascunho,
    List<ComparativoFuncaoDto> Funcoes);

internal static class RegraRascunhoPcmso
{
    // Rascunho só nasce com PGR vigente e sem outro PCMSO ativo na obra. PCMSO em vigor ou em aprovação segue pelo
    // fluxo de revisão; um rascunho que já existe não é duplicado. Obsoleto e cancelado não bloqueiam.
    public static string? Motivo(bool temPgrVigente, IReadOnlyCollection<StatusPcmsoDocumento> statusDosPcmsos)
    {
        if (!temPgrVigente) return "A obra não tem PGR vigente.";
        if (statusDosPcmsos.Contains(StatusPcmsoDocumento.Vigente))
            return "Esta obra já tem PCMSO em vigor. O médico revisa o PCMSO existente pela nova revisão.";
        if (statusDosPcmsos.Contains(StatusPcmsoDocumento.EmAprovacao)) return "Há um PCMSO em aprovação nesta obra.";
        if (statusDosPcmsos.Contains(StatusPcmsoDocumento.Rascunho)) return "Já existe um rascunho de PCMSO nesta obra.";
        return null;
    }
}

public record ObterComparativoPgrPcmsoQuery(Guid ObraId) : IRequest<ComparativoPgrPcmsoDto?>;

public class ObterComparativoPgrPcmsoQueryHandler : IRequestHandler<ObterComparativoPgrPcmsoQuery, ComparativoPgrPcmsoDto?>
{
    private readonly IAppDbContext _db;
    public ObterComparativoPgrPcmsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ComparativoPgrPcmsoDto?> Handle(ObterComparativoPgrPcmsoQuery request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId)) return null;
        var obra = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => new { o.Id, o.Nome }).FirstOrDefaultAsync(ct);
        if (obra is null) return null;

        // Tabelas isoladas e junção em memória (soft delete + INNER JOIN esconde linha).
        var pgr = await _db.Pgrs.Where(p => p.ObraId == obra.Id && p.Status == StatusPgr.Vigente)
            .OrderByDescending(p => p.DataElaboracao).Select(p => new { p.Nome }).FirstOrDefaultAsync(ct);
        var ghes = await _db.Ghes.Where(g => g.ObraId == obra.Id).Select(g => new { g.Id, g.Numero }).ToListAsync(ct);
        var idsGhes = ghes.Select(g => g.Id).ToList();
        var gheFuncoes = await _db.GheFuncoes.Where(f => idsGhes.Contains(f.GheId)).Select(f => new { f.GheId, f.FuncaoId }).ToListAsync(ct);
        var examesPorFuncao = (await _db.ExamesFuncaoObra.Where(e => e.ObraId == obra.Id).Select(e => e.FuncaoId).ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var trabalhadoresPorFuncao = (await _db.Trabalhadores.Where(t => t.ObraId == obra.Id && t.DataDemissao == null)
                .Select(t => t.FuncaoId).ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var ghesPorFuncao = gheFuncoes
            .GroupBy(f => f.FuncaoId)
            .ToDictionary(g => g.Key, g => g.Select(x => ghes.First(h => h.Id == x.GheId).Numero).Distinct().OrderBy(n => n).ToList());

        var idsFuncoes = ghesPorFuncao.Keys.Concat(examesPorFuncao.Keys).Concat(trabalhadoresPorFuncao.Keys).Distinct().ToList();
        var nomes = await _db.Funcoes.IgnoreQueryFilters().Where(f => idsFuncoes.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var linhas = new List<ComparativoFuncaoDto>();
        foreach (var id in idsFuncoes)
        {
            var noPgr = ghesPorFuncao.TryGetValue(id, out var ghesDaFuncao);
            var exames = examesPorFuncao.GetValueOrDefault(id);
            var trabalhadores = trabalhadoresPorFuncao.GetValueOrDefault(id);
            var situacao = (noPgr, exames > 0) switch
            {
                (true, true) => SituacaoFuncaoPgrPcmso.EmOrdem,
                (true, false) => SituacaoFuncaoPgrPcmso.FaltaNoPcmso,
                (false, true) => SituacaoFuncaoPgrPcmso.FaltaNoPgr,
                _ => SituacaoFuncaoPgrPcmso.FaltaNosDois,
            };
            linhas.Add(new ComparativoFuncaoDto(id, nomes.GetValueOrDefault(id) ?? "(função excluída)", ghesDaFuncao ?? new List<int>(),
                exames, trabalhadores, situacao));
        }

        var statusPcmsos = await _db.PcmsoDetalhes.Where(p => p.ObraId == obra.Id).Select(p => p.Status).ToListAsync(ct);
        return new ComparativoPgrPcmsoDto(obra.Id, obra.Nome, pgr?.Nome, RegraRascunhoPcmso.Motivo(pgr is not null, statusPcmsos),
            linhas.OrderBy(l => l.Situacao == SituacaoFuncaoPgrPcmso.EmOrdem ? 1 : 0).ThenBy(l => l.Funcao, StringComparer.CurrentCultureIgnoreCase).ToList());
    }
}

// Rascunho de PCMSO a partir do PGR vigente (botão). Preenche só funções contempladas e riscos considerados; exames,
// periodicidades, médico e CRM ficam em branco: decisão do médico (NR-07) e do usuário em 21/10, o sistema não sugere exame.
public record CriarRascunhoPcmsoDoPgrCommand(Guid ObraId) : IRequest<Guid>;

public class CriarRascunhoPcmsoDoPgrCommandHandler : IRequestHandler<CriarRascunhoPcmsoDoPgrCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarRascunhoPcmsoDoPgrCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(CriarRascunhoPcmsoDoPgrCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId))
            throw new KeyNotFoundException("Obra não encontrada. Selecione uma obra à qual você tem acesso.");

        var pgr = await _db.Pgrs.Where(p => p.ObraId == request.ObraId && p.Status == StatusPgr.Vigente)
            .OrderByDescending(p => p.DataElaboracao).Select(p => new { p.Nome, p.DataElaboracao }).FirstOrDefaultAsync(ct);
        var statusPcmsos = await _db.PcmsoDetalhes.Where(p => p.ObraId == request.ObraId).Select(p => p.Status).ToListAsync(ct);
        if (RegraRascunhoPcmso.Motivo(pgr is not null, statusPcmsos) is { } motivo)
            throw new InvalidOperationException(motivo);

        var escopo = await new ObterEscopoPcmsoQueryHandler(_db).Handle(new ObterEscopoPcmsoQuery(request.ObraId), ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");

        var funcoes = escopo.Funcoes
            .GroupBy(f => f.Funcao)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => $"{g.Key} (GHE {string.Join(", ", g.Select(x => x.GheNumero.ToString("00")).Distinct().OrderBy(n => n))})");
        var riscos = escopo.Riscos
            .GroupBy(r => r.Perigo)
            .OrderByDescending(g => g.Max(r => r.Nivel)).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => g.Key);

        var pcmso = new PcmsoDetalhe
        {
            NumeroDocumento = await _geradorNumero.GerarAsync("PCMSO", ct),
            Nome = $"PCMSO {escopo.ObraNome} · rascunho do PGR de {pgr!.DataElaboracao:dd/MM/yyyy}",
            DataEmissao = DateTime.UtcNow.Date,
            ObraId = request.ObraId,
            Status = StatusPcmsoDocumento.Rascunho,
            FuncoesContempladas = string.Join("; ", funcoes),
            RiscosConsiderados = string.Join("; ", riscos),
        };
        _db.PcmsoDetalhes.Add(pcmso);
        await _db.SaveChangesAsync(ct);
        return pcmso.Id;
    }
}
