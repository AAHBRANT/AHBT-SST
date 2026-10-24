using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Seguranca;

// Filhos acessados por id próprio (foto de participante/evidência do DDS, foto da turma, resposta de
// item de inspeção, candidato/treinamento da CIPA...) não têm ObraId nem filtro global: a obra está
// no PAI. Auditoria de 09/10/2026: esses handlers devolviam/alteravam o filho de qualquer obra.
//
// O chamador passa a consulta que projeta a ObraId do pai SEM filtro global
// (ex.: _db.Dds.IgnoreQueryFilters().Where(d => d.Id == foto.DdsId).Select(d => d.ObraId)). Sem
// filtro de propósito: o filtro global também exige Ativo, e o histórico de um pai excluído
// continuaria inacessível até para quem é da obra — aqui só a obra conta.
public static class EscopoObraPorPai
{
    public static async Task<bool> ObraDoPaiNoEscopoAsync(
        this IAppDbContext db, IQueryable<Guid> obraIdDoPai, CancellationToken ct)
    {
        if (db.EscopoObraGlobal)
            return true;

        var obras = db.ObrasNoEscopo;
        return await obraIdDoPai.AnyAsync(o => obras.Contains(o), ct);
    }

    public static async Task GarantirObraDoPaiNoEscopoAsync(
        this IAppDbContext db, IQueryable<Guid> obraIdDoPai, string mensagemNaoEncontrado, CancellationToken ct)
    {
        if (!await db.ObraDoPaiNoEscopoAsync(obraIdDoPai, ct))
            throw new KeyNotFoundException(mensagemNaoEncontrado);
    }
}
