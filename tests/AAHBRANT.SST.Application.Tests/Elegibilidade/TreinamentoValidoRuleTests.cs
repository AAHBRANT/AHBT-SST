using AAHBRANT.SST.Application.Elegibilidade.Rules;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Tests.Elegibilidade;

public class TreinamentoValidoRuleTests
{
    private static Trabalhador CriarTrabalhador() => new()
    {
        Nome = "Trabalhador Teste",
        Matricula = "MAT-0002",
        Cpf = "11111111111",
        ObraId = Guid.NewGuid(),
        FuncaoId = Guid.NewGuid(),
    };

    private static CursoTreinamento CriarCurso() => new()
    {
        Nome = "NR-35 Trabalho em Altura",
        ValidadeEmMeses = 24,
    };

    [Fact]
    public async Task Sem_treinamento_algum_nao_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var regra = new TreinamentoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.False(resultado.Atendido);
    }

    [Fact]
    public async Task Treinamento_dentro_da_validade_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        var curso = CriarCurso();
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-6),
            DataValidade = DateTime.UtcNow.AddMonths(18),
            CargaHorariaRealizada = 8,
        });
        await db.SaveChangesAsync();

        var regra = new TreinamentoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.True(resultado.Atendido);
    }

    [Fact]
    public async Task Treinamento_vencido_nao_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        var curso = CriarCurso();
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-30),
            DataValidade = DateTime.UtcNow.AddMonths(-6),
            CargaHorariaRealizada = 8,
        });
        await db.SaveChangesAsync();

        var regra = new TreinamentoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.False(resultado.Atendido);
    }
}
