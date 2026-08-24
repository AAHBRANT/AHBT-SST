using AAHBRANT.SST.Application.Elegibilidade.Rules;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Tests.Elegibilidade;

// Regra de bloqueio preventivo (§45) — sinalizada no levantamento do projeto como a mais crítica
// sem cobertura de teste: uma falha aqui libera (ou bloqueia indevidamente) um trabalhador para
// atividade de risco.
public class AsoValidoRuleTests
{
    private static Trabalhador CriarTrabalhador()
    {
        return new Trabalhador
        {
            Nome = "Trabalhador Teste",
            Matricula = "MAT-0001",
            Cpf = "00000000000",
            ObraId = Guid.NewGuid(),
            FuncaoId = Guid.NewGuid(),
        };
    }

    [Fact]
    public async Task Trabalhador_sem_aso_cadastrado_nao_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var regra = new AsoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.False(resultado.Atendido);
        Assert.True(resultado.Critico);
        Assert.Contains("não possui ASO", resultado.Detalhe);
    }

    [Fact]
    public async Task Aso_apto_e_dentro_da_validade_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-10),
            DataValidade = DateTime.UtcNow.AddDays(300),
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var regra = new AsoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.True(resultado.Atendido);
        Assert.Null(resultado.Detalhe);
    }

    [Fact]
    public async Task Aso_vencido_nao_e_atendido_mesmo_com_status_apto()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-400),
            DataValidade = DateTime.UtcNow.AddDays(-30),
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var regra = new AsoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.False(resultado.Atendido);
        Assert.Contains("vencido", resultado.Detalhe);
    }

    [Fact]
    public async Task Aso_inapto_dentro_da_validade_nao_e_atendido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-10),
            DataValidade = DateTime.UtcNow.AddDays(300),
            ResultadoStatus = ResultadoAso.Inapto,
        });
        await db.SaveChangesAsync();

        var regra = new AsoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.False(resultado.Atendido);
        Assert.Contains("Inapto", resultado.Detalhe);
    }

    [Fact]
    public async Task Considera_o_aso_mais_recente_quando_ha_mais_de_um()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador();
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Admissional,
            DataExame = DateTime.UtcNow.AddDays(-800),
            DataValidade = DateTime.UtcNow.AddDays(-400), // antigo, vencido
            ResultadoStatus = ResultadoAso.Apto,
        });
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-10), // mais recente
            DataValidade = DateTime.UtcNow.AddDays(300),
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var regra = new AsoValidoRule(db);
        var resultado = await regra.AvaliarAsync(new EligibilityRequest { TrabalhadorId = trabalhador.Id, ObraId = trabalhador.ObraId });

        Assert.True(resultado.Atendido);
    }
}
