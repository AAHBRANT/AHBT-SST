using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// 16/11/2026 (correções da revisão): início do período não pode ser anterior à abertura da etapa; "Ajustar horário" é atômico
// (substitui todos os períodos de uma vez); o instrutor da integração não é trocado pela tela de turmas.
public class AjusteDePeriodosEtapaTests
{
    private static string Dia(int diasAtras) => ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-diasAtras).ToString("yyyy-MM-dd");

    // Abre a etapa 1 em "diasAtras" (Brasília) às hh:mm:ss e tira o período automático (o técnico ajusta à mão).
    private static async Task<(CenarioIntegracao C, Guid IntegracaoId, IntegracaoEtapa Etapa)> EtapaAbertaAsync(int diasAtras, int hora, int minuto, int segundo = 0, int cargaHoras = 2)
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Nr06.CargaHorariaMinima = cargaHoras;
        await c.Db.SaveChangesAsync();
        var id = await c.IniciarAsync(c.A);
        var etapa = await c.Db.IntegracaoEtapas.SingleAsync(e => e.IntegracaoAtendimentoId == id && e.Ordem == 1);
        // Brasília é UTC-3.
        etapa.IniciadaEmUtc = ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-diasAtras).AddHours(hora + 3).AddMinutes(minuto).AddSeconds(segundo);
        c.Db.PeriodosEtapaIntegracao.RemoveRange(await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync());
        await c.Db.SaveChangesAsync();
        return (c, id, etapa);
    }

    private static void AdicionarPeriodoAutomatico(CenarioIntegracao c, IntegracaoEtapa etapa) =>
        c.Db.PeriodosEtapaIntegracao.Add(new PeriodoEtapaIntegracao
        {
            ObraId = c.Obra.Id, IntegracaoEtapaId = etapa.Id, Data = ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-1),
            Inicio = new TimeSpan(10, 0, 0), Fim = new TimeSpan(12, 0, 0), Minutos = 120, Automatico = true,
        });

    private static PeriodoAjusteEtapaIntegracao P(string data, string inicio, string fim) => new(data, inicio, fim);

    private static AjustarPeriodosEtapaIntegracaoCommandHandler Ajustar(CenarioIntegracao c) => new(c.Db);

    // ---- Item 1: início anterior à abertura da etapa, no mesmo dia ----

    [Fact]
    public async Task Lancar_InicioAntesDaAberturaNoMesmoDia_Recusa_EDaAberturaEmDianteAceita()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 10, minuto: 0, segundo: 45);
        var h = new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, Dia(1), "00:00", "08:00"), default));
        Assert.Equal("O início do período não pode ser anterior à abertura da etapa (10:00).", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, Dia(1), "09:59", "11:00"), default));

        // Abertura às 10:00:45: o minuto 10:00 vale (arredonda para baixo).
        await h.Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, Dia(1), "10:00", "12:00"), default);
        Assert.Equal(120, await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).SumAsync(p => p.Minutos));
    }

    // ---- Item 4: ajustar horário atômico ----

    [Fact]
    public async Task Ajustar_SubstituiTodosOsPeriodos_EDeixamDeSerAutomaticos()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 10, minuto: 0);
        AdicionarPeriodoAutomatico(c, etapa);
        await c.Db.SaveChangesAsync();

        // Informados fora de ordem: a gravação ordena.
        await Ajustar(c).Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(1), "13:00", "15:00"), P(Dia(1), "10:30", "12:30")]), default);

        var periodos = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).OrderBy(p => p.Inicio).ToListAsync();
        Assert.Equal(2, periodos.Count);
        Assert.All(periodos, p => Assert.False(p.Automatico));
        Assert.Equal(new TimeSpan(10, 30, 0), periodos[0].Inicio);
        Assert.Equal(240, periodos.Sum(p => p.Minutos));
    }

    [Fact]
    public async Task Ajustar_ComUmPeriodoInvalido_NaoMudaNada()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 10, minuto: 0);
        AdicionarPeriodoAutomatico(c, etapa);
        await c.Db.SaveChangesAsync();
        var h = Ajustar(c);

        // Sobreposição dentro da lista.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(1), "10:00", "11:00"), P(Dia(1), "10:30", "12:00")]), default));
        // O segundo começa antes da abertura (10:00).
        var antes = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(1), "10:00", "11:00"), P(Dia(1), "08:00", "09:00")]), default));
        Assert.Contains("anterior à abertura da etapa (10:00)", antes.Message);
        // Fim no futuro.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(1), "10:00", "11:00"), P("2099-01-01", "08:00", "09:00")]), default));
        // Mais de 12 h.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(1), "10:00", "23:00")]), default));
        // Lista vazia.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id, []), default));

        var periodo = await c.Db.PeriodosEtapaIntegracao.SingleAsync(p => p.IntegracaoEtapaId == etapa.Id);
        Assert.True(periodo.Automatico);
        Assert.Equal(120, periodo.Minutos);
    }

    [Fact]
    public async Task Ajustar_JanelaQueCruzaAMeiaNoite_ContaOMinutoDaVirada()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 2, hora: 22, minuto: 0);

        await Ajustar(c).Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id,
            [P(Dia(2), "22:00", "23:59"), P(Dia(1), "00:00", "06:00")]), default);

        var periodos = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).OrderBy(p => p.Data).ToListAsync();
        Assert.Equal(2, periodos.Count);
        Assert.Equal(480, periodos.Sum(p => p.Minutos)); // 22:00 às 06:00
    }

    [Fact]
    public async Task Ajustar_EtapaForaDeAndamento_Recusa()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 10, minuto: 0);
        etapa.Status = StatusEtapaIntegracao.Concluida;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Ajustar(c).Handle(
            new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id, [P(Dia(1), "10:00", "12:00")]), default));
    }

    // ---- Item 5: instrutor único da integração ----

    [Fact]
    public async Task TurmaDeIntegracao_NaoTrocaOInstrutorPelaTelaDeTurmas()
    {
        var (c, _, etapa) = await EtapaAbertaAsync(diasAtras: 0, hora: 8, minuto: 0);
        var outro = await c.NovaPessoaAsync(c.Ajudante, "Outro Instrutor");
        c.Db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = outro.Id });
        await c.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirInstrutorSessaoTreinamentoCommandHandler(c.Db)
            .Handle(new DefinirInstrutorSessaoTreinamentoCommand(etapa.SessaoTreinamentoId!.Value, outro.Id), default));
        Assert.Equal("O instrutor desta turma é o da integração e não pode ser trocado aqui.", ex.Message);
    }
}
