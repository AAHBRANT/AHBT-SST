using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    private static (List<CursoTreinamento> Cursos, List<Treinamento> Treinamentos, List<Aso> Asos)
        ConstruirTreinamentosEAsos(List<Trabalhador> trabalhadores, DateTime referenciaUtc)
    {
        var cursos = CatalogoCursosNr
            .Select(c => new CursoTreinamento
            {
                Nome = c.Nome,
                NormaReferencia = c.NormaReferencia,
                CargaHorariaMinima = c.CargaHorariaMinima,
                ValidadeEmMeses = c.ValidadeEmMeses,
            })
            .ToList();

        var treinamentos = new List<Treinamento>();
        var asos = new List<Aso>();
        var indiceGlobal = 0;

        foreach (var trabalhador in trabalhadores)
        {
            var codigosCursos = DistribuicaoFuncoes.Single(f => f.Funcao == trabalhador.Funcao!.Nome).CodigosCursos;
            foreach (var codigoCurso in codigosCursos)
            {
                var curso = cursos.Single(c => c.NormaReferencia == codigoCurso);
                var dataValidade = DistribuidorFaixaVencimento.CalcularData(indiceGlobal, referenciaUtc);
                treinamentos.Add(new Treinamento
                {
                    Trabalhador = trabalhador,
                    CursoTreinamento = curso,
                    DataRealizacao = dataValidade.AddMonths(-curso.ValidadeEmMeses),
                    DataValidade = dataValidade,
                    CargaHorariaRealizada = curso.CargaHorariaMinima,
                    InstituicaoInstrutor = "SENAI - Unidade Construção Civil",
                    NumeroCertificado = $"CERT-{codigoCurso}-{trabalhador.Matricula}",
                });
                indiceGlobal++;
            }

            var dataValidadeAso = DistribuidorFaixaVencimento.CalcularData(indiceGlobal, referenciaUtc);
            // ~5% dos trabalhadores (1 em cada 20) representam admissões recentes na obra — os
            // demais já passaram pelo admissional em algum momento anterior e estão no periódico.
            var tipoExame = indiceGlobal % 20 == 0
                ? TipoExameAso.Admissional
                : TipoExameAso.Periodico;
            asos.Add(new Aso
            {
                Trabalhador = trabalhador,
                Tipo = tipoExame,
                DataExame = dataValidadeAso.AddYears(-1),
                DataValidade = dataValidadeAso,
                ResultadoStatus = ResultadoAso.Apto,
                MedicoNome = "Dr. Marcelo Andrade",
                MedicoCrm = "CRM-MG 45231",
            });
            indiceGlobal++;
        }

        return (cursos, treinamentos, asos);
    }
}
