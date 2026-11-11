using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes.AtividadesFuncao;

// PreexistentesIgnorados: pares que já tinham registro (sugeridos ou decididos) e por isso não foram tocados.
public record ResultadoSugestaoAtividadesFuncao(int Sugeridos, int PreexistentesIgnorados, int ChamadasIa, int TokensEntrada, int TokensSaida);

// Uma análise da IA por GHE. Grava só pares SEM registro, como Pendente: o que o engenheiro já decidiu
// (Validada ou Descartada) e o que já estava sugerido nunca é alterado.
public record SugerirAtividadesFuncaoGheCommand(Guid ObraId, Guid GheId) : IRequest<ResultadoSugestaoAtividadesFuncao>;

public class SugerirAtividadesFuncaoGheCommandHandler : IRequestHandler<SugerirAtividadesFuncaoGheCommand, ResultadoSugestaoAtividadesFuncao>
{
    // Limites por chamada; acima disso a análise é dividida em blocos.
    public const int MaxAtividadesPorChamada = 80;
    public const int MaxFuncoesPorChamada = 30;

    private readonly IAppDbContext _db;
    private readonly IAnalistaAtividadesFuncao _analista;

    public SugerirAtividadesFuncaoGheCommandHandler(IAppDbContext db, IAnalistaAtividadesFuncao analista)
    {
        _db = db;
        _analista = analista;
    }

    private sealed record FuncaoDoGhe(string Codigo, Guid FuncaoId, string Nome, string? Descricao);

    public async Task<ResultadoSugestaoAtividadesFuncao> Handle(SugerirAtividadesFuncaoGheCommand request, CancellationToken ct)
    {
        var ghe = await _db.Ghes.FirstOrDefaultAsync(g => g.Id == request.GheId && g.ObraId == request.ObraId, ct)
            ?? throw new KeyNotFoundException($"GHE {request.GheId} não encontrado.");

        var atividades = await _db.Atividades.EmUso(_db)
            .Where(a => a.ObraId == request.ObraId && a.GheId == ghe.Id)
            .OrderBy(a => a.Nome).Select(a => new { a.Id, a.Nome }).ToListAsync(ct);
        var gheFuncoes = await _db.GheFuncoes.Where(gf => gf.GheId == ghe.Id)
            .Select(gf => new { gf.FuncaoId, gf.DescricaoAtividades }).ToListAsync(ct);
        if (atividades.Count == 0 || gheFuncoes.Count == 0)
            return new ResultadoSugestaoAtividadesFuncao(0, 0, 0, 0, 0);

        var nomes = await NomesFuncoes.CarregarAsync(_db, gheFuncoes.Select(f => f.FuncaoId), ct);
        var atividadeIds = atividades.Select(a => a.Id).ToList();
        var existentes = (await _db.AtividadesFuncoes.Where(x => atividadeIds.Contains(x.AtividadeId))
                .Select(x => new { x.AtividadeId, x.FuncaoId }).ToListAsync(ct))
            .Select(x => (x.AtividadeId, x.FuncaoId)).ToHashSet();

        // Código curto por atividade e nome único por função, só para esta análise.
        var codigoDaAtividade = atividades.Select((a, i) => (Codigo: $"A{i + 1:000}", a.Id, a.Nome)).ToList();
        var porCodigo = codigoDaAtividade.ToDictionary(x => x.Codigo);
        // Função também por código curto: duas funções com nome parecido não se confundem.
        var funcoes = gheFuncoes
            .Select(f => (f.FuncaoId, Nome: nomes.GetValueOrDefault(f.FuncaoId) ?? string.Empty, f.DescricaoAtividades))
            .Where(f => f.Nome.Length > 0)
            .Select((f, i) => new FuncaoDoGhe($"F{i + 1:00}", f.FuncaoId, f.Nome, f.DescricaoAtividades)).ToList();

        bool SemRegistro(Guid funcaoId, Guid atividadeId) => !existentes.Contains((atividadeId, funcaoId));
        var funcoesComLacuna = funcoes.Where(f => atividades.Any(a => SemRegistro(f.FuncaoId, a.Id))).ToList();
        var atividadesComLacuna = codigoDaAtividade.Where(a => funcoes.Any(f => SemRegistro(f.FuncaoId, a.Id))).ToList();
        var totalPares = atividades.Count * funcoes.Count;
        var paresSemRegistro = atividades.Sum(a => funcoes.Count(f => SemRegistro(f.FuncaoId, a.Id)));

        int sugeridos = 0, chamadas = 0, tokensEntrada = 0, tokensSaida = 0;
        var novos = new HashSet<(Guid, Guid)>();

        foreach (var blocoAtividades in atividadesComLacuna.Chunk(MaxAtividadesPorChamada))
        foreach (var blocoFuncoes in funcoesComLacuna.Chunk(MaxFuncoesPorChamada))
        {
            var codigosDoBloco = blocoAtividades.Select(a => a.Codigo).ToHashSet();
            var contexto = new ContextoAtividadesFuncao(
                ghe.Numero, ghe.DescricaoAmbiente,
                blocoFuncoes.Select(f => new FuncaoParaAnalise(f.Codigo, f.Nome, f.Descricao)).ToList(),
                blocoAtividades.Select(a => new AtividadeParaAnalise(a.Codigo, a.Nome)).ToList());

            var resposta = await _analista.SugerirAsync(contexto, ct);
            chamadas++;
            tokensEntrada += resposta.TokensEntrada;
            tokensSaida += resposta.TokensSaida;

            foreach (var porFuncao in resposta.Funcoes ?? Array.Empty<SugestaoFuncaoIa>())
            {
                // Função que a IA inventou (fora da entrada) é descartada.
                var funcao = blocoFuncoes.FirstOrDefault(f => string.Equals(f.Codigo, porFuncao.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (funcao is null) continue;
                foreach (var s in porFuncao.Atividades ?? new List<SugestaoAtividadeIa>())
                {
                    // Código inventado ou de outro bloco é descartado.
                    var codigo = s.Codigo?.Trim().ToUpperInvariant();
                    if (codigo is null || !codigosDoBloco.Contains(codigo)) continue;
                    var atividadeId = porCodigo[codigo].Id;
                    if (!SemRegistro(funcao.FuncaoId, atividadeId) || !novos.Add((atividadeId, funcao.FuncaoId))) continue;

                    _db.AtividadesFuncoes.Add(new AtividadeFuncao
                    {
                        ObraId = request.ObraId,
                        AtividadeId = atividadeId,
                        FuncaoId = funcao.FuncaoId,
                        Status = StatusMedidaPrevencao.Pendente,
                        OrigemVinculo = OrigemMedidaPrevencao.SugestaoIa,
                        JustificativaIa = Limitar(s.Justificativa, 300),
                    });
                    sugeridos++;
                }
            }
        }

        if (sugeridos > 0) await ConflitoAtividadesFuncao.SalvarAsync(_db, ct);
        return new ResultadoSugestaoAtividadesFuncao(sugeridos, totalPares - paresSemRegistro, chamadas, tokensEntrada, tokensSaida);
    }

    private static string? Limitar(string? texto, int maximo)
    {
        var limpo = (texto ?? string.Empty).Trim();
        if (limpo.Length == 0) return null;
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd();
    }
}

// Nome das funções em consulta separada: função excluída (soft delete) não pode sumir da matriz.
internal static class NomesFuncoes
{
    public static async Task<Dictionary<Guid, string>> CarregarAsync(IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Funcoes.IgnoreQueryFilters().Where(f => lista.Contains(f.Id))
            .Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
    }
}
