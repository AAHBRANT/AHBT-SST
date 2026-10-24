using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Minimo;

public static class SituacaoEstoqueEpi
{
    public const string EmDia = "emDia";
    public const string Atencao = "atencao";
    public const string AbaixoMinimo = "abaixoMinimo";
    public const string Zerado = "zerado";
    public const string SemMinimo = "semMinimo";

    public static bool PedeReposicao(string situacao) => situacao is AbaixoMinimo or Zerado;
}

public record SugestaoTransferenciaEpiDto(Guid ObraId, string ObraNome, int Quantidade);

public record EstoqueEpiPainelItemDto(
    Guid CatalogoEpiId, Guid? EstoqueEpiId, string Nome, string? Fabricante, string? CertificadoAprovacaoNumero,
    DateTime? CertificadoAprovacaoValidade, bool TemFoto, int Saldo, int Reservado, int Disponivel, int? EstoqueMinimo,
    int MinimoSugerido, int ConsumoUltimos90Dias, string Situacao, int ReservasEmFalta, IReadOnlyList<string> TamanhosEmFalta,
    int QuantidadeSugeridaCompra, DateTime? EstoqueMinimoDefinidoEm, string? EstoqueMinimoDefinidoPor,
    IReadOnlyList<SugestaoTransferenciaEpiDto> Transferencias);

// Especificação 21/10/2026, Módulo 2 — tudo que a aba Estoque, o alerta de mínimo e a solicitação de
// reposição precisam saber de cada EPI numa obra, calculado num lugar só para os três nunca divergirem.
//
// Disponível = saldo físico − kits separados para funcionários novos (ReservaEpi ativa).
// Mínimo sugerido = consumo diário × prazo de reposição da obra + estoque de segurança, onde:
//   consumo diário = (saídas por entrega − devoluções) dos últimos 90 dias ÷ 90; obra sem histórico usa
//                    a vida útil do EPI: pessoas que usam o EPI ÷ (vida útil em dias) — cada pessoa troca
//                    o item uma vez por vida útil;
//   segurança      = admissões dos últimos 30 dias em funções que usam o EPI (o ritmo de contratação),
//                    no mínimo 2 unidades.
// EPI que ninguém na obra usa e que não teve saída fica com sugestão 0.
public static class CalculadoraEstoqueEpi
{
    public const int DiasHistorico = 90;
    public const int DiasAdmissoesRecentes = 30;
    public const int SegurancaMinima = 2;

    public static async Task<List<EstoqueEpiPainelItemDto>> CalcularAsync(
        IAppDbContext db, Guid obraId, CancellationToken ct, bool comTransferencias = true)
    {
        var obra = await db.Obras.Where(o => o.Id == obraId).Select(o => new { o.PrazoReposicaoEpiDias }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");
        var prazo = obra.PrazoReposicaoEpiDias > 0 ? obra.PrazoReposicaoEpiDias : 15;
        var agora = DateTime.UtcNow;

        var catalogo = await db.CatalogoEpis
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id, c.Nome, c.Fabricante, c.CertificadoAprovacaoNumero, c.CertificadoAprovacaoValidade, c.VidaUtilEmMeses,
                TemFoto = c.FotoConteudo != null,
            })
            .ToListAsync(ct);
        var estoques = await db.EstoquesEpi
            .Where(e => e.ObraId == obraId)
            .Select(e => new
            {
                e.Id, e.CatalogoEpiId, e.Saldo, e.EstoqueMinimo, e.EstoqueMinimoDefinidoEm,
                DefinidoPor = e.EstoqueMinimoDefinidoPorUsuario != null ? e.EstoqueMinimoDefinidoPorUsuario.Nome : null,
            })
            .ToDictionaryAsync(e => e.CatalogoEpiId, ct);

        var desde = agora.AddDays(-DiasHistorico);
        var movimentos = await db.MovimentacoesEstoqueEpi
            .Where(m => m.EstoqueEpi!.ObraId == obraId && m.CreatedAtUtc >= desde
                && (m.Tipo == TipoMovimentacaoEstoqueEpi.SaidaEntrega || m.Tipo == TipoMovimentacaoEstoqueEpi.DevolucaoEntrada))
            .GroupBy(m => new { m.EstoqueEpi!.CatalogoEpiId, m.Tipo })
            .Select(g => new { g.Key.CatalogoEpiId, g.Key.Tipo, Total = g.Sum(m => m.Quantidade) })
            .ToListAsync(ct);

        var reservas = await db.ReservasEpi
            .Where(r => r.ObraId == obraId && r.Status == StatusReservaEpi.Ativa)
            .OrderBy(r => r.CreatedAtUtc).ThenBy(r => r.Id)
            .Select(r => new { r.CatalogoEpiId, r.Quantidade, r.Tamanho })
            .ToListAsync(ct);

        var trabalhadores = await db.Trabalhadores
            .Where(t => t.ObraId == obraId && t.Situacao != SituacaoTrabalhador.Desligado && t.DataDemissao == null)
            .Select(t => new { t.FuncaoId, t.DataAdmissao })
            .ToListAsync(ct);
        var matriz = await db.MatrizEpiFuncoes.Select(m => new { m.FuncaoId, m.CatalogoEpiId }).ToListAsync(ct);
        var funcoesPorEpi = matriz.GroupBy(m => m.CatalogoEpiId).ToDictionary(g => g.Key, g => g.Select(m => m.FuncaoId).ToHashSet());
        var limiteAdmissao = agora.Date.AddDays(-DiasAdmissoesRecentes);

        var itens = new List<EstoqueEpiPainelItemDto>();
        foreach (var c in catalogo)
        {
            estoques.TryGetValue(c.Id, out var estoque);
            var saldo = estoque?.Saldo ?? 0;
            var filaReservas = reservas.Where(r => r.CatalogoEpiId == c.Id).ToList();
            var reservado = filaReservas.Sum(r => r.Quantidade);
            var disponivel = saldo - reservado;

            // Reservas que passam do saldo físico (fila por ordem de chegada) = kits em falta.
            var restante = saldo;
            var emFalta = new List<string?>();
            foreach (var r in filaReservas)
            {
                if (restante < r.Quantidade) emFalta.Add(r.Tamanho);
                restante -= r.Quantidade;
            }
            var tamanhosEmFalta = emFalta.Where(t => !string.IsNullOrWhiteSpace(t))
                .GroupBy(t => t!).OrderBy(g => g.Key).Select(g => g.Count() > 1 ? $"{g.Key} ({g.Count()})" : g.Key).ToList();

            var saidas = movimentos.Where(m => m.CatalogoEpiId == c.Id && m.Tipo == TipoMovimentacaoEstoqueEpi.SaidaEntrega).Sum(m => m.Total);
            var devolucoes = movimentos.Where(m => m.CatalogoEpiId == c.Id && m.Tipo == TipoMovimentacaoEstoqueEpi.DevolucaoEntrada).Sum(m => m.Total);
            var consumo = Math.Max(0, saidas - devolucoes);

            var funcoes = funcoesPorEpi.GetValueOrDefault(c.Id) ?? new HashSet<Guid>();
            var usuarios = trabalhadores.Count(t => funcoes.Contains(t.FuncaoId));
            var admissoesRecentes = trabalhadores.Count(t => funcoes.Contains(t.FuncaoId) && t.DataAdmissao.Date >= limiteAdmissao);

            var consumoDiario = consumo > 0
                ? consumo / (double)DiasHistorico
                : usuarios / (Math.Max(1, c.VidaUtilEmMeses) * 30.0);
            var sugerido = consumo == 0 && usuarios == 0
                ? 0
                : (int)Math.Ceiling(consumoDiario * prazo + Math.Max(SegurancaMinima, admissoesRecentes));

            var minimo = estoque?.EstoqueMinimo;
            var situacao = Situacao(disponivel, minimo, emFalta.Count);
            var compra = minimo is > 0 ? Math.Max(0, 2 * minimo.Value - disponivel) : Math.Max(0, -disponivel);

            itens.Add(new EstoqueEpiPainelItemDto(
                c.Id, estoque?.Id, c.Nome, c.Fabricante, c.CertificadoAprovacaoNumero, c.CertificadoAprovacaoValidade, c.TemFoto,
                saldo, reservado, disponivel, minimo, sugerido, consumo, situacao, emFalta.Count, tamanhosEmFalta,
                compra, estoque?.EstoqueMinimoDefinidoEm, estoque?.DefinidoPor, Array.Empty<SugestaoTransferenciaEpiDto>()));
        }

        if (comTransferencias && itens.Any(i => SituacaoEstoqueEpi.PedeReposicao(i.Situacao)))
            itens = await ComTransferenciasAsync(db, obraId, itens, ct);
        return itens;
    }

    public static string Situacao(int disponivel, int? minimo, int reservasEmFalta)
    {
        if (minimo is null || minimo == 0)
            return reservasEmFalta > 0 ? SituacaoEstoqueEpi.Zerado : minimo is null ? SituacaoEstoqueEpi.SemMinimo : SituacaoEstoqueEpi.EmDia;
        if (disponivel <= 0) return SituacaoEstoqueEpi.Zerado;
        if (disponivel < minimo) return SituacaoEstoqueEpi.AbaixoMinimo;
        if (disponivel < minimo * 1.5) return SituacaoEstoqueEpi.Atencao;
        return SituacaoEstoqueEpi.EmDia;
    }

    // Outra obra com sobra do mesmo EPI (disponível acima do próprio mínimo): sugere transferir o que
    // falta, até o tamanho da sobra. Só obras que o usuário enxerga (filtro global de Obra).
    private static async Task<List<EstoqueEpiPainelItemDto>> ComTransferenciasAsync(
        IAppDbContext db, Guid obraId, List<EstoqueEpiPainelItemDto> itens, CancellationToken ct)
    {
        var pedem = itens.Where(i => SituacaoEstoqueEpi.PedeReposicao(i.Situacao)).Select(i => i.CatalogoEpiId).ToList();
        var outras = await db.EstoquesEpi
            .Where(e => e.ObraId != obraId && pedem.Contains(e.CatalogoEpiId) && e.Saldo > 0 && e.Obra!.Ativo)
            .Select(e => new { e.ObraId, ObraNome = e.Obra!.Nome, e.CatalogoEpiId, e.Saldo, Minimo = e.EstoqueMinimo ?? 0 })
            .ToListAsync(ct);
        var obrasVisiveis = await db.Obras.Select(o => o.Id).ToListAsync(ct);
        var reservadoOutras = await db.ReservasEpi
            .Where(r => r.ObraId != obraId && pedem.Contains(r.CatalogoEpiId) && r.Status == StatusReservaEpi.Ativa)
            .GroupBy(r => new { r.ObraId, r.CatalogoEpiId })
            .Select(g => new { g.Key.ObraId, g.Key.CatalogoEpiId, Total = g.Sum(r => r.Quantidade) })
            .ToListAsync(ct);

        return itens.Select(i =>
        {
            if (!pedem.Contains(i.CatalogoEpiId)) return i;
            var falta = i.QuantidadeSugeridaCompra;
            var sugestoes = outras
                .Where(o => o.CatalogoEpiId == i.CatalogoEpiId && obrasVisiveis.Contains(o.ObraId))
                .Select(o =>
                {
                    var reservado = reservadoOutras.FirstOrDefault(r => r.ObraId == o.ObraId && r.CatalogoEpiId == o.CatalogoEpiId)?.Total ?? 0;
                    var sobra = o.Saldo - reservado - (int)Math.Ceiling(o.Minimo * 1.5);
                    return new SugestaoTransferenciaEpiDto(o.ObraId, o.ObraNome, Math.Min(Math.Max(0, sobra), falta));
                })
                .Where(s => s.Quantidade > 0)
                .OrderByDescending(s => s.Quantidade)
                .ToList();
            return i with { Transferencias = sugestoes };
        }).ToList();
    }
}
