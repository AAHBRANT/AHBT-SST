using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Um único documento de assinatura por registro de origem (EntidadeTipo, EntidadeId), mesmo com várias
// requisições ao mesmo tempo — vários tablets confirmando presença no mesmo DDS, ou a tela pedindo o
// documento duas vezes. Antes era "consulta e, se não houver, cria", e duas requisições no mesmo
// instante criavam dois documentos (11 casos em produção até 11/11/2026, todos no mesmo segundo):
// as assinaturas iam para um e a consulta podia devolver o outro, vazio.
//
// Quem garante é o índice único filtrado (ver DocumentoAssinaturaConfiguracao). Quem perde a corrida
// recebe a violação do índice, descarta a sua inserção e devolve o documento que venceu.
public static class DocumentoAssinaturaUnico
{
    public static async Task<DocumentoAssinatura> ObterOuCriarAsync(
        IAppDbContext db, string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        var existente = await BuscarAsync(db, entidadeTipo, entidadeId, ct);
        if (existente is not null)
            return existente;

        var novo = new DocumentoAssinatura { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId };
        db.DocumentosAssinatura.Add(novo);
        try
        {
            await db.SaveChangesAsync(ct);
            return novo;
        }
        catch (DbUpdateException)
        {
            // Tira a inserção recusada do rastreamento: o mesmo contexto ainda grava a assinatura logo
            // depois, e sem isto tentaria inserir o documento duplicado de novo.
            db.DocumentosAssinatura.Entry(novo).State = EntityState.Detached;

            var vencedor = await BuscarAsync(db, entidadeTipo, entidadeId, ct);
            // Nenhum documento concorrente: a falha foi outra, e não deve ser escondida.
            if (vencedor is null)
                throw;
            return vencedor;
        }
    }

    private static Task<DocumentoAssinatura?> BuscarAsync(IAppDbContext db, string entidadeTipo, Guid entidadeId, CancellationToken ct)
        => db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == entidadeTipo && d.EntidadeId == entidadeId, ct);
}
