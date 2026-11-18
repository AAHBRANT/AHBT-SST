using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Troca de função sem ASO de mudança de risco: só avisa o SESMT (NR-7, item 7.5.10), não bloqueia.
public class TrocaFuncaoSemAsoAlertaProviderTests
{
    static TrocaFuncaoSemAsoAlertaProviderTests() => ChavesCpfDeTeste.Configurar();

    private static readonly DateTime DataDaTroca = DateTime.UtcNow.AddDays(-5);

    private static async Task<(SstDbContext Db, Trabalhador Trabalhador, HistoricoFuncaoTrabalhador Troca, Funcao Carpinteiro)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        var carpinteiro = new Funcao { Nome = "Carpinteiro" };
        var trabalhador = new Trabalhador { ObraId = obra.Id, FuncaoId = carpinteiro.Id, Nome = "Dailton", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow.AddYears(-1) };
        var troca = new HistoricoFuncaoTrabalhador
        {
            TrabalhadorId = trabalhador.Id, FuncaoAnteriorId = pedreiro.Id, FuncaoNovaId = carpinteiro.Id, AlteradaEm = DataDaTroca,
        };
        db.Obras.Add(obra);
        db.Funcoes.AddRange(pedreiro, carpinteiro);
        db.Trabalhadores.Add(trabalhador);
        db.HistoricosFuncaoTrabalhador.Add(troca);
        await db.SaveChangesAsync();
        return (db, trabalhador, troca, carpinteiro);
    }

    private static Aso AsoDe(Guid trabalhadorId, TipoExameAso tipo, DateTime dataExame) => new()
    {
        TrabalhadorId = trabalhadorId, Tipo = tipo, DataExame = dataExame, DataValidade = dataExame.AddYears(1),
    };

    private static Alerta AlertaDaTroca(Guid trocaId, StatusAlerta status) => new()
    {
        Tipo = TipoAlerta.TrocaFuncaoSemAsoMudancaRisco, Severidade = SeveridadeAlerta.Critico, Titulo = "x",
        EntidadeOrigemTipo = TrocaFuncaoSemAsoAlertaProvider.EntidadeOrigemTipo, EntidadeOrigemId = trocaId, Status = status,
    };

    [Fact]
    public async Task TrocaSemAsoDeMudanca_GeraAlertaComAsFuncoes()
    {
        var (db, trabalhador, troca, _) = await PrepararAsync();

        var item = Assert.Single(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());

        Assert.Equal(troca.Id, item.EntidadeOrigemId);
        Assert.Equal(trabalhador.Id, item.TrabalhadorId);
        Assert.Equal(trabalhador.ObraId, item.ObraId);
        Assert.Equal(DataDaTroca.Date, item.DataVencimento);
        Assert.Equal(TipoAlerta.TrocaFuncaoSemAsoMudancaRisco, item.TipoAlertaVencido);
        Assert.Contains("Dailton", item.Titulo);
        Assert.Contains("Pedreiro", item.Descricao);
        Assert.Contains("Carpinteiro", item.Descricao);
        Assert.False(item.Substituido);
    }

    [Fact]
    public async Task AsoDeMudancaFeitoPoucoAntesDaTroca_NaoGeraAlerta()
    {
        var (db, trabalhador, _, _) = await PrepararAsync();
        db.Asos.Add(AsoDe(trabalhador.Id, TipoExameAso.MudancaDeFuncao, DataDaTroca.AddDays(-3)));
        await db.SaveChangesAsync();

        Assert.Empty(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());
    }

    [Fact]
    public async Task AsoPeriodicoOuAsoDeMudancaAntigo_NaoAtendeATroca()
    {
        var (db, trabalhador, _, _) = await PrepararAsync();
        db.Asos.Add(AsoDe(trabalhador.Id, TipoExameAso.Periodico, DataDaTroca.AddDays(1)));
        db.Asos.Add(AsoDe(trabalhador.Id, TipoExameAso.MudancaDeFuncao,
            DataDaTroca.AddDays(-TrocaFuncaoSemAsoAlertaProvider.DiasAntecedenciaAceitaDoAso - 30)));
        await db.SaveChangesAsync();

        Assert.Single(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());
    }

    [Fact]
    public async Task AsoLancadoDepoisDoAlerta_DevolveOItemComoSubstituidoParaOMotorEncerrar()
    {
        var (db, trabalhador, troca, _) = await PrepararAsync();
        db.Alertas.Add(AlertaDaTroca(troca.Id, StatusAlerta.Aberto));
        db.Asos.Add(AsoDe(trabalhador.Id, TipoExameAso.MudancaDeFuncao, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var item = Assert.Single(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());

        Assert.Equal(troca.Id, item.EntidadeOrigemId);
        Assert.True(item.Substituido);
    }

    [Theory]
    [InlineData(StatusAlerta.Resolvido)]
    [InlineData(StatusAlerta.Ignorado)]
    public async Task AlertaEncerradoPeloSesmt_NaoVolta(StatusAlerta status)
    {
        var (db, _, troca, _) = await PrepararAsync();
        db.Alertas.Add(AlertaDaTroca(troca.Id, status));
        await db.SaveChangesAsync();

        Assert.Empty(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());
    }

    [Fact]
    public async Task NovaTroca_ValeSoAMaisRecenteEAAnteriorEEncerrada()
    {
        var (db, trabalhador, trocaAntiga, carpinteiro) = await PrepararAsync();
        var encarregado = new Funcao { Nome = "Encarregado" };
        var trocaNova = new HistoricoFuncaoTrabalhador
        {
            TrabalhadorId = trabalhador.Id, FuncaoAnteriorId = carpinteiro.Id, FuncaoNovaId = encarregado.Id, AlteradaEm = DateTime.UtcNow.AddDays(-1),
        };
        db.Funcoes.Add(encarregado);
        db.HistoricosFuncaoTrabalhador.Add(trocaNova);
        db.Alertas.Add(AlertaDaTroca(trocaAntiga.Id, StatusAlerta.Aberto));
        await db.SaveChangesAsync();

        var itens = await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync();

        Assert.Equal(2, itens.Count);
        Assert.False(itens.Single(i => i.EntidadeOrigemId == trocaNova.Id).Substituido);
        Assert.True(itens.Single(i => i.EntidadeOrigemId == trocaAntiga.Id).Substituido);
    }

    [Fact]
    public async Task TrabalhadorDesligado_NaoGeraAlerta()
    {
        var (db, trabalhador, _, _) = await PrepararAsync();
        trabalhador.Ativo = false;
        await db.SaveChangesAsync();

        Assert.Empty(await new TrocaFuncaoSemAsoAlertaProvider(db).ObterItensAsync());
    }
}
