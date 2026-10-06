using AAHBRANT.SST.Application.CursosTreinamento.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Tests.CursosTreinamento;

public class ListarCursosNr6SemMarcadorQueryHandlerTests
{
    static ListarCursosNr6SemMarcadorQueryHandlerTests() => ChavesCpfDeTeste.Configurar();

    [Theory]
    [InlineData("NR-06 e NR-18", true)]
    [InlineData("NR 06/NR 35", true)]
    [InlineData("nr6 e outras", true)]
    [InlineData("6", true)]
    [InlineData("NR-35", false)]
    [InlineData("NR-16", false)]
    [InlineData("NR-36", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NormaMencionaNr6_ReconheceCitacoesMasNaoOutrasNormas(string? norma, bool esperado)
        => Assert.Equal(esperado, CursoTreinamento.NormaMencionaNr6(norma));

    [Fact]
    public async Task ListaSoCursoQueCitaNr6SemMarcador_ContandoTrabalhadoresComCertificadoValido()
    {
        var db = DbContextFactory.Criar();
        var composto = new CursoTreinamento { Nome = "EPI + Altura", NormaReferencia = "NR-06 e NR-35", AtendeNr6 = false };
        var correto = new CursoTreinamento { Nome = "NR-06 Uso de EPI", NormaReferencia = "NR-06", AtendeNr6 = true };
        var outro = new CursoTreinamento { Nome = "NR-35", NormaReferencia = "NR-35", AtendeNr6 = false };
        db.CursosTreinamento.AddRange(composto, correto, outro);

        var funcao = new Funcao { Nome = "Pedreiro" };
        var t1 = new Trabalhador { ObraId = Guid.NewGuid(), Funcao = funcao, Nome = "A", Cpf = "00000000001" };
        var t2 = new Trabalhador { ObraId = Guid.NewGuid(), Funcao = funcao, Nome = "B", Cpf = "00000000002" };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(t1, t2);
        await db.SaveChangesAsync();

        db.Treinamentos.AddRange(
            new Treinamento { TrabalhadorId = t1.Id, CursoTreinamentoId = composto.Id, DataRealizacao = DateTime.UtcNow.AddMonths(-1), DataValidade = DateTime.UtcNow.AddYears(1) },
            // Segundo certificado válido do MESMO trabalhador no mesmo curso não conta em dobro.
            new Treinamento { TrabalhadorId = t1.Id, CursoTreinamentoId = composto.Id, DataRealizacao = DateTime.UtcNow.AddMonths(-2), DataValidade = DateTime.UtcNow.AddYears(1) },
            // Vencido não conta.
            new Treinamento { TrabalhadorId = t2.Id, CursoTreinamentoId = composto.Id, DataRealizacao = DateTime.UtcNow.AddYears(-3), DataValidade = DateTime.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var handler = new ListarCursosNr6SemMarcadorQueryHandler(db);
        var resultado = await handler.Handle(new ListarCursosNr6SemMarcadorQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal(composto.Id, item.Id);
        Assert.Equal(1, item.TrabalhadoresComCertificadoValido);
    }

    [Fact]
    public async Task SemCursosProblematicos_RetornaListaVazia()
    {
        var db = DbContextFactory.Criar();
        db.CursosTreinamento.Add(new CursoTreinamento { Nome = "NR-06", NormaReferencia = "NR-06", AtendeNr6 = true });
        await db.SaveChangesAsync();

        var resultado = await new ListarCursosNr6SemMarcadorQueryHandler(db)
            .Handle(new ListarCursosNr6SemMarcadorQuery(), default);

        Assert.Empty(resultado);
    }
}
