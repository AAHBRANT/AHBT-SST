using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Ocorrência assinada por duas pessoas (pedido do usuário, 23/11/2026): o Técnico de Segurança (digital ou
// facial, como antes) e o Engenheiro de Segurança (login do Teams). O documento só fecha com as duas; quem
// assina é classificado pela função cadastrada do trabalhador.
public static class AssinaturasOcorrencia
{
    public enum Papel { Tecnico, Engenheiro }

    public record Situacao(bool TecnicoAssinou, bool EngenheiroAssinou)
    {
        public bool Completa => TecnicoAssinou && EngenheiroAssinou;
    }

    public static Papel? Classificar(string? nomeFuncao) =>
        FuncaoSstClassifier.EhEngenheiroSeguranca(nomeFuncao) ? Papel.Engenheiro
        : FuncaoSstClassifier.EhTecnicoSeguranca(nomeFuncao) ? Papel.Tecnico
        : null;

    // IgnoreQueryFilters: o engenheiro pode ser de outra obra, e quem assinou e depois mudou de obra
    // continua valendo.
    public static async Task<Situacao> ObterAsync(IAppDbContext db, Guid documentoAssinaturaId, CancellationToken ct)
    {
        var funcoes = await db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documentoAssinaturaId && s.Ativo)
            .Join(db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => t.Funcao != null ? t.Funcao.Nome : null)
            .ToListAsync(ct);
        var papeis = funcoes.Select(Classificar).ToList();
        return new Situacao(papeis.Contains(Papel.Tecnico), papeis.Contains(Papel.Engenheiro));
    }
}
