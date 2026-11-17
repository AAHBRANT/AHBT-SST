using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
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

    private static PeriodoAjusteEtapaIntegracao P(string data, string inicio, string fim) => new(data, inicio, fim);

    private const string MsgBloqueio = "A carga horária é contada automaticamente desde a abertura da etapa; não há lançamento manual.";

    // ---- 17/11/2026: nenhum lançamento manual ----

    [Fact]
    public async Task Registrar_Ajustar_Remover_SaoBloqueados_ENadaMuda()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 10, minuto: 0);
        await PeriodosAutomaticosEtapa.GarantirAsync(c.Db, await c.Db.IntegracoesAtendimento.SingleAsync(a => a.Id == id), etapa, default);
        await c.Db.SaveChangesAsync();
        var antes = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync();

        var e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => new RegistrarPeriodoEtapaIntegracaoCommandHandler(c.Db)
            .Handle(new RegistrarPeriodoEtapaIntegracaoCommand(id, etapa.Id, Dia(1), "10:00", "12:00"), default));
        var e2 = await Assert.ThrowsAsync<InvalidOperationException>(() => new AjustarPeriodosEtapaIntegracaoCommandHandler(c.Db)
            .Handle(new AjustarPeriodosEtapaIntegracaoCommand(id, etapa.Id, [P(Dia(1), "10:00", "12:00")]), default));
        var e3 = await Assert.ThrowsAsync<InvalidOperationException>(() => new RemoverPeriodoEtapaIntegracaoCommandHandler(c.Db)
            .Handle(new RemoverPeriodoEtapaIntegracaoCommand(id, etapa.Id, antes[0].Id), default));

        Assert.All(new[] { e1, e2, e3 }, e => Assert.Equal(MsgBloqueio, e.Message));
        var depois = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync();
        Assert.Equal(antes.Select(p => p.Id).OrderBy(x => x), depois.Select(p => p.Id).OrderBy(x => x));
        Assert.All(depois, p => Assert.True(p.Automatico));
    }

    // ---- Recontagem: manual (como na foto do tablet) vira automático desde a abertura ----

    [Fact]
    public async Task Recontagem_SubstituiManualPeloAutomatico_DesdeAAbertura()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 6, minuto: 31, cargaHoras: 8);
        c.Db.PeriodosEtapaIntegracao.Add(new PeriodoEtapaIntegracao
        {
            ObraId = c.Obra.Id, IntegracaoEtapaId = etapa.Id, Data = ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-1),
            Inicio = new TimeSpan(6, 31, 0), Fim = new TimeSpan(13, 12, 0), Minutos = 401, Automatico = false,
        });
        await c.Db.SaveChangesAsync();

        var dto = await c.ObterAsync(id);

        var periodo = Assert.Single(dto.Etapas[0].Periodos);
        Assert.True(periodo.Automatico);
        Assert.Equal("06:31", periodo.Inicio);
        Assert.Equal("14:31", periodo.Fim);
        Assert.Equal(480, dto.Etapas[0].CargaLancadaMinutos);
    }

    [Fact]
    public async Task Recontagem_SoEmEtapasAbertasNaRegraNova_AntigasMantemOManual()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 6, minuto: 31, cargaHoras: 8);
        var at = await c.Db.IntegracoesAtendimento.SingleAsync(a => a.Id == id);
        c.Db.PeriodosEtapaIntegracao.Add(new PeriodoEtapaIntegracao
        {
            ObraId = c.Obra.Id, IntegracaoEtapaId = etapa.Id, Data = ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-1),
            Inicio = new TimeSpan(6, 31, 0), Fim = new TimeSpan(13, 12, 0), Minutos = 401, Automatico = false,
        });
        await c.Db.SaveChangesAsync();

        // Etapa aberta ANTES da regra (corte depois da abertura): o manual fica.
        Assert.False(await PeriodosAutomaticosEtapa.GarantirAsync(c.Db, at, etapa, default, etapa.IniciadaEmUtc!.Value.AddMinutes(1)));
        Assert.Equal(401, (await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync()).Sum(p => p.Minutos));

        // Aberta NA regra nova (corte antes da abertura): recontada.
        Assert.True(await PeriodosAutomaticosEtapa.GarantirAsync(c.Db, at, etapa, default, etapa.IniciadaEmUtc!.Value.AddMinutes(-1)));
        await c.Db.SaveChangesAsync();
        Assert.Equal(480, (await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync()).Sum(p => p.Minutos));
    }

    [Fact]
    public async Task Leitura_NaoMexeNosPeriodos_SeATurmaDaEtapaJaFoiConcluida()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 1, hora: 6, minuto: 31, cargaHoras: 8);
        (await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == etapa.SessaoTreinamentoId)).Status = StatusSessaoTreinamento.Concluida;
        await c.Db.SaveChangesAsync();

        await c.ObterAsync(id);

        Assert.Empty(await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync());
    }

    [Fact]
    public async Task Recontagem_JanelaQueCruzaAMeiaNoite_DivideEContaOsMinutosTodos()
    {
        var (c, id, etapa) = await EtapaAbertaAsync(diasAtras: 2, hora: 22, minuto: 0, cargaHoras: 8);

        await c.ObterAsync(id);

        var periodos = await c.Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).OrderBy(p => p.Data).ToListAsync();
        Assert.Equal(2, periodos.Count);
        Assert.All(periodos, p => Assert.True(p.Automatico));
        Assert.Equal(480, periodos.Sum(p => p.Minutos));
    }

    [Fact]
    public void CalcularAte_ContaDaAberturaAteAgora_NoMinuto_ComMinimoDeUmMinuto()
    {
        var abertura = new DateTime(2026, 11, 17, 9, 31, 20, DateTimeKind.Utc); // 06:31 em Brasília
        var p = Assert.Single(PeriodosAutomaticosEtapa.CalcularAte(abertura, abertura.AddHours(6).AddMinutes(41).AddSeconds(5)));
        Assert.Equal(new TimeSpan(6, 31, 0), p.Inicio);
        Assert.Equal(401, p.Minutos);
        Assert.Equal(1, Assert.Single(PeriodosAutomaticosEtapa.CalcularAte(abertura, abertura.AddSeconds(10))).Minutos);
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
