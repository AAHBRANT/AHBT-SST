using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static List<MatrizEpiFuncao> ConstruirMatrizEpiFuncao(List<Funcao> funcoes, List<CatalogoEpi> catalogosEpi)
    {
        var matriz = new List<MatrizEpiFuncao>();
        foreach (var (nomeFuncao, nomesEpis) in MatrizEpiPorFuncao)
        {
            var funcao = funcoes.Single(f => f.Nome == nomeFuncao);
            foreach (var nomeEpi in nomesEpis)
            {
                var epi = catalogosEpi.Single(c => c.Nome == nomeEpi);
                matriz.Add(new MatrizEpiFuncao { Funcao = funcao, CatalogoEpi = epi });
            }
        }
        return matriz;
    }
}
