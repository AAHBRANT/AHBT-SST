using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    // Diferente de ConstruirMatrizEpiFuncao (que usa uma tabela própria MatrizEpiPorFuncao), a
    // matriz de treinamento reaproveita diretamente o CodigosCursos já presente em
    // DistribuicaoFuncoes (MockObraSeeder.DadosEstaticos.cs) — mesma fonte usada para gerar os
    // Treinamentos individuais em ConstruirTreinamentosEAsos.
    private static List<MatrizTreinamentoFuncao> ConstruirMatrizTreinamentoFuncao(List<Funcao> funcoes, List<CursoTreinamento> cursos)
    {
        var matriz = new List<MatrizTreinamentoFuncao>();
        foreach (var (nomeFuncao, _, codigosCursos) in DistribuicaoFuncoes)
        {
            var funcao = funcoes.Single(f => f.Nome == nomeFuncao);
            foreach (var codigoCurso in codigosCursos)
            {
                var curso = cursos.Single(c => c.NormaReferencia == codigoCurso);
                matriz.Add(new MatrizTreinamentoFuncao { Funcao = funcao, CursoTreinamento = curso });
            }
        }
        return matriz;
    }
}
