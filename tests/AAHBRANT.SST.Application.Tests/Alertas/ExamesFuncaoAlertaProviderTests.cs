using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class ExamesFuncaoAlertaProviderTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    private static IAppDbContext CriarDb(string nome) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, new CurrentUserService());

    private static async Task<(Trabalhador T, Guid ObraId, Guid FuncaoId)> SemearAsync(IAppDbContext db)
    {
        var obraId = Guid.NewGuid();
        var funcaoId = Guid.NewGuid();
        var t = new Trabalhador { Id = Guid.NewGuid(), Nome = "Pedro", ObraId = obraId, FuncaoId = funcaoId, DataAdmissao = Hoje.AddYears(-2) };
        db.Trabalhadores.Add(t);
        db.ExamesFuncaoObra.AddRange(
            new ExameFuncaoObra { ObraId = obraId, FuncaoId = funcaoId, Exame = "AUDIOMETRIA TONAL", CodigoExame = "0281", Periodico = true, PeriodicidadeMeses = 12 },
            new ExameFuncaoObra { ObraId = obraId, FuncaoId = funcaoId, Exame = "GLICEMIA EM JEJUM", CodigoExame = "0658", Periodico = true, PeriodicidadeMeses = 12 },
            new ExameFuncaoObra { ObraId = obraId, FuncaoId = funcaoId, Exame = "EXAME CLINICO", CodigoExame = "0295", Periodico = true, PeriodicidadeMeses = 12 },
            // Só admissional: não entra no alerta de periódico.
            new ExameFuncaoObra { ObraId = obraId, FuncaoId = funcaoId, Exame = "RAIO-X", CodigoExame = "1415", Periodico = false, Admissional = true });
        await db.SaveChangesAsync();
        return (t, obraId, funcaoId);
    }

    [Fact]
    public async Task Um_item_por_trabalhador_com_vencimento_do_exame_mais_urgente()
    {
        var db = CriarDb(nameof(Um_item_por_trabalhador_com_vencimento_do_exame_mais_urgente));
        var (t, _, _) = await SemearAsync(db);
        // Audiometria com código: vence daqui a ~10 dias.
        db.ExamesComplementares.Add(new ExameComplementar { TrabalhadorId = t.Id, Tipo = TipoExameComplementar.Audiometria, CodigoExame = "0281", DataRealizacao = Hoje.AddMonths(-12).AddDays(10), DataValidade = Hoje, Resultado = "Normal" });
        // Exame clínico coberto pelo ASO de 2 meses atrás.
        db.Asos.Add(new Aso { TrabalhadorId = t.Id, DataExame = Hoje.AddMonths(-2), DataValidade = Hoje.AddMonths(10) });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ExamesFuncaoAlertaProvider(db).ObterItensAsync());

        // Glicemia nunca registrada vence na admissão: é a mais urgente.
        Assert.Equal(t.DataAdmissao, item.DataVencimento);
        Assert.Equal(t.Id, item.EntidadeOrigemId);
        Assert.Contains("GLICEMIA EM JEJUM: nunca registrado", item.Descricao);
        Assert.Contains("AUDIOMETRIA TONAL: vence em", item.Descricao);
        Assert.DoesNotContain("EXAME CLINICO", item.Descricao);
        Assert.DoesNotContain("RAIO-X", item.Descricao);
        Assert.Equal("2 exames do PCMSO de Pedro", item.Titulo);
    }

    [Fact]
    public async Task Registro_antigo_sem_codigo_conta_so_quando_a_categoria_e_inequivoca()
    {
        var db = CriarDb(nameof(Registro_antigo_sem_codigo_conta_so_quando_a_categoria_e_inequivoca));
        var (t, _, _) = await SemearAsync(db);
        db.ExamesComplementares.AddRange(
            new ExameComplementar { TrabalhadorId = t.Id, Tipo = TipoExameComplementar.Audiometria, DataRealizacao = Hoje.AddMonths(-1), DataValidade = Hoje.AddMonths(11), Resultado = "Normal" },
            // "Laboratoriais" não diz se foi glicemia: não conta.
            new ExameComplementar { TrabalhadorId = t.Id, Tipo = TipoExameComplementar.Laboratoriais, DataRealizacao = Hoje.AddMonths(-1), DataValidade = Hoje.AddMonths(11), Resultado = "Normal" });
        db.Asos.Add(new Aso { TrabalhadorId = t.Id, DataExame = Hoje.AddMonths(-1), DataValidade = Hoje.AddMonths(11) });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ExamesFuncaoAlertaProvider(db).ObterItensAsync());

        Assert.Equal("• GLICEMIA EM JEJUM: nunca registrado", item.Descricao);
        Assert.Equal("Exame do PCMSO de Pedro", item.Titulo);
    }

    [Fact]
    public async Task Tudo_em_dia_fica_sem_descricao_e_vencimento_futuro()
    {
        var db = CriarDb(nameof(Tudo_em_dia_fica_sem_descricao_e_vencimento_futuro));
        var (t, _, _) = await SemearAsync(db);
        db.ExamesComplementares.AddRange(
            new ExameComplementar { TrabalhadorId = t.Id, Tipo = TipoExameComplementar.Audiometria, CodigoExame = "0281", DataRealizacao = Hoje.AddMonths(-1), DataValidade = Hoje, Resultado = "Normal" },
            new ExameComplementar { TrabalhadorId = t.Id, Tipo = TipoExameComplementar.Laboratoriais, NomeExame = "Glicemia em jejum", DataRealizacao = Hoje.AddMonths(-2), DataValidade = Hoje, Resultado = "Normal" });
        db.Asos.Add(new Aso { TrabalhadorId = t.Id, DataExame = Hoje.AddMonths(-3), DataValidade = Hoje.AddMonths(9) });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ExamesFuncaoAlertaProvider(db).ObterItensAsync());

        Assert.Null(item.Descricao);
        Assert.Equal(Hoje.AddMonths(-3).AddMonths(12), item.DataVencimento);
    }

    [Fact]
    public async Task Alerta_aberto_de_quem_saiu_do_quadro_volta_como_substituido()
    {
        var db = CriarDb(nameof(Alerta_aberto_de_quem_saiu_do_quadro_volta_como_substituido));
        await SemearAsync(db);
        var desligadoId = Guid.NewGuid();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.ExameFuncaoVencido,
            Severidade = SeveridadeAlerta.Critico,
            Titulo = "VENCIDO: Exame do PCMSO de Fulano",
            EntidadeOrigemTipo = ExamesFuncaoAlertaProvider.EntidadeOrigemTipo,
            EntidadeOrigemId = desligadoId,
            Status = StatusAlerta.Aberto,
        });
        await db.SaveChangesAsync();

        var itens = await new ExamesFuncaoAlertaProvider(db).ObterItensAsync();

        Assert.True(itens.Single(i => i.EntidadeOrigemId == desligadoId).Substituido);
        Assert.Equal(2, itens.Count);
    }
}
