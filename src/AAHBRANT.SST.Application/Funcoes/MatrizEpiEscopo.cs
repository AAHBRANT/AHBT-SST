using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Funcoes;

// Matriz de EPI por obra (04/11/2026). Cada obra pode ter a própria matriz, criada ao aprovar o PGR dela. A matriz antiga
// (ObraId nulo) segue valendo, função por função, enquanto a obra não tiver linhas próprias para aquela função: quem está
// numa obra com matriz própria nunca enxerga a matriz de outra obra, e nada muda para as obras que ainda não têm a sua.
public static class MatrizEpiEscopo
{
    public static IQueryable<MatrizEpiFuncao> ParaObra(this IQueryable<MatrizEpiFuncao> matriz, Guid obraId)
        => matriz.Where(m => m.ObraId == obraId
            || (m.ObraId == null && !matriz.Any(x => x.ObraId == obraId && x.FuncaoId == m.FuncaoId)));
}
