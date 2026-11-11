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

// Etapa 3: a admissão põe o funcionário nas turmas dos cursos da função que NÃO são do pacote de integração
// (NR-06, NR-18, NR-35, NR-12 e Integração de Segurança saíram da ficha: são do fluxo "Realizar integração").
public class AgendaTreinamentosIntegracaoTests
{
    private sealed record Cenario(SstDbContext Db, Obra Obra, Funcao Funcao, CursoTreinamento Integracao, CursoTreinamento Nr6, CursoTreinamento Nr35, CursoTreinamento Nr10)
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

    // A função exige NR-35 (pacote) e NR-10 (fora do pacote).
    private static async Task<Cenario> MontarAsync(string funcaoNome = "Armador")
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "AUR", Nome = "Edifício Aurora" };
        var funcao = new Funcao { Nome = funcaoNome };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var nr6 = new CursoTreinamento { Nome = "NR-06 EPI", NormaReferencia = "NR-06", CargaHorariaMinima = 4, ValidadeEmMeses = 24, AtendeNr6 = true };
        var nr35 = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var nr10 = new CursoTreinamento { Nome = "NR-10 Segurança em Eletricidade", NormaReferencia = "NR-10", CargaHorariaMinima = 40, ValidadeEmMeses = 24 };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(integracao, nr6, nr35, nr10);
        db.MatrizTreinamentoFuncoes.AddRange(
            new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = nr35.Id },
            new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = nr10.Id });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, funcao, integracao, nr6, nr35, nr10);
    }

    [Fact]
    public void SomarDiasUteis_PulaFimDeSemana()
    {
        // 23/10/2026 é sexta-feira: dois dias úteis depois é terça, 27/10.
        Assert.Equal(new DateTime(2026, 10, 27), AgendaTreinamentosIntegracao.SomarDiasUteis(new DateTime(2026, 10, 23), 2));
    }

    [Fact]
    public async Task Admissao_NaoAgendaOPacote_MasAgendaOCursoForaDoPacote_ComOFuncionarioInscrito()
    {
        var c = await MontarAsync();
        var joao = await c.AdmitirAsync();

        var turmas = await c.Db.SessoesTreinamento.Include(s => s.Participantes).ToListAsync();
        // Só NR-10: Integração, NR-06 e NR-35 (pacote) não geram turma na ficha.
        var turma = Assert.Single(turmas);
        Assert.Equal(c.Nr10.Id, turma.CursoTreinamentoId);
        Assert.Contains(turma.Participantes, p => p.TrabalhadorId == joao.Id);
        Assert.DoesNotContain(turmas, t => t.CursoTreinamentoId == c.Integracao.Id || t.CursoTreinamentoId == c.Nr6.Id || t.CursoTreinamentoId == c.Nr35.Id);

        var esperada = AgendaTreinamentosIntegracao.SomarDiasUteis(AgendaTreinamentosIntegracao.HojeBrasilia(), AgendaTreinamentosIntegracao.DiasUteisDemaisTurmas);
        Assert.Equal(esperada, turma.DataRealizacao);
        // Padrão da NR: carga mínima e número de certificado.
        Assert.Equal(40, turma.CargaHorariaRealizada);
        Assert.StartsWith("CERT-", turma.NumeroCertificado);
    }

    [Fact]
    public async Task CursoComNormaDoPacoteEmFormatoLivre_TambemNaoEhAgendado()
    {
        var c = await MontarAsync();
        var nr18 = new CursoTreinamento { Nome = "Condições e Meio Ambiente — NR 18", NormaReferencia = "NR 18", CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        var nr12 = new CursoTreinamento { Nome = "Máquinas e Equipamentos", NormaReferencia = "nr-12", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        c.Db.CursosTreinamento.AddRange(nr18, nr12);
        c.Db.MatrizTreinamentoFuncoes.AddRange(
            new MatrizTreinamentoFuncao { FuncaoId = c.Funcao.Id, CursoTreinamentoId = nr18.Id },
            new MatrizTreinamentoFuncao { FuncaoId = c.Funcao.Id, CursoTreinamentoId = nr12.Id });
        await c.Db.SaveChangesAsync();

        await c.AdmitirAsync();

        var turma = Assert.Single(await c.Db.SessoesTreinamento.ToListAsync());
        Assert.Equal(c.Nr10.Id, turma.CursoTreinamentoId);
    }

    [Fact]
    public async Task SegundoFuncionario_EntraNaMesmaTurmaAberta()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync("Primeiro");
        await c.AdmitirAsync("Segundo");

        var turmas = await c.Db.SessoesTreinamento.Include(s => s.Participantes).ToListAsync();
        var turma = Assert.Single(turmas);
        Assert.Equal(2, turma.Participantes.Count);
    }

    [Fact]
    public async Task QuemJaTemOTreinamentoValido_NaoEntraNaTurma()
    {
        var c = await MontarAsync();
        var joao = new Trabalhador { ObraId = c.Obra.Id, FuncaoId = c.Funcao.Id, Nome = "João", Cpf = "00000000000", DataAdmissao = DateTime.UtcNow.Date };
        c.Db.Trabalhadores.Add(joao);
        c.Db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = joao.Id, CursoTreinamentoId = c.Nr10.Id, DataRealizacao = DateTime.UtcNow.Date.AddMonths(-2),
            DataValidade = DateTime.UtcNow.Date.AddMonths(22), CargaHorariaRealizada = 40,
        });
        await c.Db.SaveChangesAsync();

        await c.Servico.SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);

        Assert.Empty(await c.Db.SessoesTreinamento.ToListAsync());
    }

    [Fact]
    public async Task TecnicoDeSeguranca_NaoEhAgendado()
    {
        var c = await MontarAsync("Técnico de Segurança do Trabalho");
        await c.AdmitirAsync();

        Assert.Empty(await c.Db.SessoesTreinamento.ToListAsync());
    }

    // Sem calendário: o aviso é por obra/dia e as turmas fora do pacote aparecem na descrição.
    [Fact]
    public async Task Admissao_NaoMandaEventoAoCalendario_E_OAvisoListaOsTreinamentosForaDoPacote()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync();

        Assert.Empty(c.Calendario.Mensagens);
        var alerta = await c.Db.Alertas.FirstAsync();
        Assert.Contains("Treinamentos agendados: NR-10 Segurança em Eletricidade (1)", alerta.Descricao);
        Assert.DoesNotContain("NR-35", alerta.Descricao);
    }

    [Fact]
    public async Task AlterarData_MudaATurma_AlertaAgrupado_NaoTemEventoParaAtualizar()
    {
        var c = await MontarAsync();
        await c.AdmitirAsync();
        var turma = await c.Db.SessoesTreinamento.SingleAsync(t => t.CursoTreinamentoId == c.Nr10.Id);
        var novaData = AgendaTreinamentosIntegracao.HojeBrasilia().AddDays(1);
        var calendario = new FilaCalendarioTeamsFalsa();

        await new AlterarDataSessaoTreinamentoCommandHandler(c.Db, calendario)
            .Handle(new AlterarDataSessaoTreinamentoCommand(turma.Id, novaData), default);

        Assert.Equal(novaData, (await c.Db.SessoesTreinamento.SingleAsync(t => t.Id == turma.Id)).DataRealizacao);
        // O alerta agrupado da obra não pertence a uma pessoa nem tem evento de calendário.
        Assert.Empty(calendario.Mensagens);
    }

    // Alertas antigos (um por pessoa, com evento de calendário) continuam sendo atualizados.
    [Fact]
    public async Task AlterarData_AlertaAntigoPorPessoa_AtualizaOEventoDoCalendario()
    {
        var c = await MontarAsync();
        var joao = await c.AdmitirAsync();
        var turma = await c.Db.SessoesTreinamento.SingleAsync(t => t.CursoTreinamentoId == c.Nr10.Id);
        foreach (var usuarioId in c.Tecnicos.UsuarioIdsARetornar)
            c.Db.Alertas.Add(new Alerta
            {
                Tipo = TipoAlerta.IntegracaoFuncionarioNovo, Titulo = "Funcionário novo: João da Silva", Descricao = "Aviso antigo",
                EntidadeOrigemTipo = nameof(PlanoIntegracao), EntidadeOrigemId = Guid.NewGuid(),
                TrabalhadorId = joao.Id, ObraId = c.Obra.Id, DestinatarioUsuarioId = usuarioId,
            });
        await c.Db.SaveChangesAsync();
        var novaData = AgendaTreinamentosIntegracao.HojeBrasilia().AddDays(1);
        var calendario = new FilaCalendarioTeamsFalsa();

        await new AlterarDataSessaoTreinamentoCommandHandler(c.Db, calendario)
            .Handle(new AlterarDataSessaoTreinamentoCommand(turma.Id, novaData), default);

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
        var turma = await c.Db.SessoesTreinamento.SingleAsync(t => t.CursoTreinamentoId == c.Nr10.Id);
        var participante = await c.Db.ParticipantesSessaoTreinamento.FirstAsync(p => p.SessaoTreinamentoId == turma.Id);
        participante.PresencaConfirmadaEm = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AlterarDataSessaoTreinamentoCommandHandler(c.Db, new FilaCalendarioTeamsFalsa())
            .Handle(new AlterarDataSessaoTreinamentoCommand(turma.Id, AgendaTreinamentosIntegracao.HojeBrasilia().AddDays(5)), default));
    }
}
