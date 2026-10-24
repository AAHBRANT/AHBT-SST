using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.PlanosIntegracao;

// Etapa 3: a admissão já põe o funcionário nas turmas da função e manda o convite ao calendário.
public class AgendaTreinamentosIntegracaoTests
{
    private sealed record Cenario(SstDbContext Db, Obra Obra, Funcao Funcao, CursoTreinamento Integracao, CursoTreinamento Nr6, CursoTreinamento Nr35)
    {
        public FilaCalendarioTeamsFalsa Calendario { get; } = new();
        public TecnicosSegurancaPorObraServiceFalso Tecnicos { get; } = new() { UsuarioIdsARetornar = { Guid.NewGuid(), Guid.NewGuid() } };
        public PlanoIntegracaoService Servico => new(Db, Tecnicos, new FilaNotificacaoTeamsFalsa(), Calendario, new GeradorNumeroSequencialFalso());

        public async Task<Trabalhador> AdmitirAsync(string nome = "João da Silva", string funcaoNome = "")
        {
            var t = new Trabalhador { ObraId = Obra.Id, FuncaoId = Funcao.Id, Nome = nome, Cpf = "00000000000", DataAdmissao = DateTime.UtcNow.Date };
            Db.Trabalhadores.Add(t);
            await Db.SaveChangesAsync();
            await Servico.SincronizarAsync(t.Id, MotivoPlanoIntegracao.Admissao);
            return t;
        }
    }

    private static async Task<Cenario> MontarAsync(string funcaoNome = "Armador")
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "AUR", Nome = "Edifício Aurora" };
        var funcao = new Funcao { Nome = funcaoNome };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var nr6 = new CursoTreinamento { Nome = "NR-06 EPI", NormaReferencia = "NR-06", CargaHorariaMinima = 4, ValidadeEmMeses = 24, AtendeNr6 = true };
        var nr35 = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(integracao, nr6, nr35);
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = nr35.Id });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, funcao, integracao, nr6, nr35);
    }

    [Fact]
    public void SomarDiasUteis_PulaFimDeSemana()
    {
        // 23/10/2026 é sexta-feira: dois dias úteis depois é terça, 27/10.
        Assert.Equal(new DateTime(2026, 10, 27), AgendaTreinamentosIntegracao.SomarDiasUteis(new DateTime(2026, 10, 23), 2));
    }

    [Fact]
    public async Task Admissao_CriaTurmasDaIntegracaoNr06EDemaisCursos_ComOFuncionarioInscrito()
    {
        var c = await MontarAsync();
        var joao = await c.AdmitirAsync();

        var turmas = await c.Db.SessoesTreinamento.Include(s => s.Participantes).ToListAsync();
        Assert.Equal(3, turmas.Count);
        Assert.All(turmas, t => Assert.Contains(t.Participantes, p => p.TrabalhadorId == joao.Id));

        var hoje = AgendaTreinamentosIntegracao.HojeBrasilia();
        var primeiras = AgendaTreinamentosIntegracao.SomarDiasUteis(hoje, AgendaTreinamentosIntegracao.DiasUteisPrimeirasTurmas);
        Assert.Equal(primeiras, turmas.Single(t => t.CursoTreinamentoId == c.Nr6.Id).DataRealizacao);
        Assert.Equal(primeiras, turmas.Single(t => t.CursoTreinamentoId == c.Integracao.Id).DataRealizacao);
        Assert.True(turmas.Single(t => t.CursoTreinamentoId == c.Nr35.Id).DataRealizacao > primeiras);
        // Padrão da NR: carga mínima e número de certificado.
        Assert.Equal(8, turmas.Single(t => t.CursoTreinamentoId == c.Nr35.Id).CargaHorariaRealizada);
        Assert.All(turmas, t => Assert.StartsWith("CERT-", t.NumeroCertificado));
    }

    [Fact]
    public async Task SegundoFuncionario_EntraNaMesmaTurmaAberta()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync("Primeiro");
        await c.AdmitirAsync("Segundo");

        var turmas = await c.Db.SessoesTreinamento.Include(s => s.Participantes).ToListAsync();
        Assert.Equal(3, turmas.Count);
        Assert.All(turmas, t => Assert.Equal(2, t.Participantes.Count));
    }

    [Fact]
    public async Task QuemJaTemOTreinamentoValido_NaoEntraNaTurma()
    {
        var c = await MontarAsync();
        var joao = new Trabalhador { ObraId = c.Obra.Id, FuncaoId = c.Funcao.Id, Nome = "João", Cpf = "00000000000", DataAdmissao = DateTime.UtcNow.Date };
        c.Db.Trabalhadores.Add(joao);
        c.Db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = joao.Id, CursoTreinamentoId = c.Nr35.Id, DataRealizacao = DateTime.UtcNow.Date.AddMonths(-2),
            DataValidade = DateTime.UtcNow.Date.AddMonths(22), CargaHorariaRealizada = 8,
        });
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        Assert.DoesNotContain(await c.Db.SessoesTreinamento.ToListAsync(), t => t.CursoTreinamentoId == c.Nr35.Id);
    }

    [Fact]
    public async Task TecnicoDeSeguranca_NaoEhAgendado()
    {
        var c = await MontarAsync("Técnico de Segurança do Trabalho");
        await c.AdmitirAsync();

        Assert.Empty(await c.Db.SessoesTreinamento.ToListAsync());
    }

    [Fact]
    public async Task Admissao_MandaConviteAoCalendarioDeCadaTecnico_NaDataDaPrimeiraTurma()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync();

        var primeiras = AgendaTreinamentosIntegracao.SomarDiasUteis(AgendaTreinamentosIntegracao.HojeBrasilia(), 2);
        Assert.Equal(2, c.Calendario.Mensagens.Count);
        Assert.All(c.Calendario.Mensagens, m =>
        {
            Assert.Equal("Alerta", m.EntidadeOrigemTipo);
            Assert.Equal(OperacaoCalendarioTeams.Criar, m.Operacao);
            Assert.Equal(primeiras, m.Data);
            Assert.Contains("João da Silva", m.Titulo);
        });
        var alerta = await c.Db.Alertas.FirstAsync();
        Assert.Contains("Treinamentos agendados:", alerta.Descricao);
    }

    [Fact]
    public async Task AlterarData_MudaATurma_E_AtualizaOEventoDoCalendario()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync();
        var turmaNr6 = await c.Db.SessoesTreinamento.SingleAsync(t => t.CursoTreinamentoId == c.Nr6.Id);
        var novaData = AgendaTreinamentosIntegracao.HojeBrasilia().AddDays(1);
        var calendario = new FilaCalendarioTeamsFalsa();

        await new AlterarDataSessaoTreinamentoCommandHandler(c.Db, calendario)
            .Handle(new AlterarDataSessaoTreinamentoCommand(turmaNr6.Id, novaData), default);

        Assert.Equal(novaData, (await c.Db.SessoesTreinamento.SingleAsync(t => t.Id == turmaNr6.Id)).DataRealizacao);
        Assert.Equal(2, calendario.Mensagens.Count);
        Assert.All(calendario.Mensagens, m =>
        {
            Assert.Equal(OperacaoCalendarioTeams.Atualizar, m.Operacao);
            Assert.Equal(novaData, m.Data);
        });
    }

    [Fact]
    public async Task AlterarData_ComPresencaRegistrada_EhRecusado()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync();
        var turma = await c.Db.SessoesTreinamento.SingleAsync(t => t.CursoTreinamentoId == c.Nr6.Id);
        var participante = await c.Db.ParticipantesSessaoTreinamento.FirstAsync(p => p.SessaoTreinamentoId == turma.Id);
        participante.PresencaConfirmadaEm = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AlterarDataSessaoTreinamentoCommandHandler(c.Db, new FilaCalendarioTeamsFalsa())
            .Handle(new AlterarDataSessaoTreinamentoCommand(turma.Id, AgendaTreinamentosIntegracao.HojeBrasilia().AddDays(5)), default));
    }
}
