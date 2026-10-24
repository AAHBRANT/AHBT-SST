using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.Terceirizados;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.PlanosIntegracao;

// Pop-up "Funcionário novo": a ordem dos passos e a situação de cada um vêm dos dados reais.
public class ListarPlanosIntegracaoPendentesQueryTests
{
    private static async Task<(SstDbContext db, Trabalhador joao, CursoTreinamento integracao, CursoTreinamento nr6, CursoTreinamento nr35, CatalogoEpi bota)> MontarAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "AUR", Nome = "Edifício Aurora" };
        var funcao = new Funcao { Nome = "Armador" };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var nr6 = new CursoTreinamento { Nome = "NR-06", NormaReferencia = "NR-06", CargaHorariaMinima = 4, ValidadeEmMeses = 24, AtendeNr6 = true };
        var nr35 = new CursoTreinamento { Nome = "NR-35", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var bota = new CatalogoEpi { Nome = "Bota de Segurança", VidaUtilEmMeses = 12 };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(integracao, nr6, nr35);
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = nr35.Id });
        db.CatalogoEpis.Add(bota);
        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = bota.Id });
        db.EstoquesEpi.Add(new EstoqueEpi { ObraId = obra.Id, CatalogoEpiId = bota.Id, Saldo = 3 });
        var joao = new Trabalhador { ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João da Silva", Cpf = "00000000000", DataAdmissao = DateTime.UtcNow.Date, TamanhoCalcadoEpi = "42" };
        db.Trabalhadores.Add(joao);
        await db.SaveChangesAsync();
        await new PlanoIntegracaoService(db, new TecnicosSegurancaPorObraServiceFalso(), new FilaNotificacaoTeamsFalsa(), new FilaCalendarioTeamsFalsa(), new GeradorNumeroSequencialFalso())
            .SincronizarAsync(joao.Id, MotivoPlanoIntegracao.Admissao);
        return (db, joao, integracao, nr6, nr35, bota);
    }

    private static Task<List<PlanoIntegracaoDto>> ListarAsync(SstDbContext db) =>
        new ListarPlanosIntegracaoPendentesQueryHandler(db).Handle(new ListarPlanosIntegracaoPendentesQuery(), default);

    private static Treinamento Valido(Trabalhador t, CursoTreinamento c) => new()
    {
        TrabalhadorId = t.Id, CursoTreinamentoId = c.Id, DataRealizacao = DateTime.UtcNow.Date,
        DataValidade = DateTime.UtcNow.Date.AddYears(1), CargaHorariaRealizada = c.CargaHorariaMinima,
    };

    [Fact]
    public async Task FuncionarioRecemChegado_PassosNaOrdemCerta_TurmasAgendadas_E_KitSeparado()
    {
        var (db, _, _, _, _, _) = await MontarAsync();

        var plano = Assert.Single(await ListarAsync(db));

        Assert.Equal(new[] { "aso", "biometria", "integracao", "nr06", "cursos", "kit" }, plano.Passos.Select(p => p.Codigo));
        Assert.Equal(SituacaoPasso.Pendente, plano.Passos[0].Situacao);
        Assert.Contains("Facial pendente · digital pendente", plano.Passos[1].Detalhe);
        // A admissão já agendou as turmas (etapa 3).
        Assert.Equal(SituacaoPasso.Agendado, plano.Passos[2].Situacao);
        Assert.Equal(SituacaoPasso.Agendado, plano.Passos[3].Situacao);
        Assert.NotNull(plano.Passos[3].SessaoTreinamentoId);
        Assert.Equal(SituacaoPasso.Agendado, plano.Passos[4].Situacao);
        Assert.Contains("NR-35", plano.Passos[4].Detalhe);
        // Kit só é liberado depois da NR-06.
        Assert.Equal(SituacaoPasso.Aguardando, plano.Passos[5].Situacao);
        var item = Assert.Single(plano.Kit);
        Assert.Equal("separado", item.Situacao);
        Assert.Equal("42", item.Tamanho);
        Assert.Equal(0, plano.PassosConcluidos);
    }

    [Fact]
    public async Task ParticipanteDeTurmaAberta_ApareceComoAgendado()
    {
        var (db, joao, _, nr6, _, _) = await MontarAsync();
        var turma = new SessaoTreinamento { ObraId = joao.ObraId, CursoTreinamentoId = nr6.Id, DataRealizacao = new DateTime(2026, 10, 22), CargaHorariaRealizada = 4 };
        turma.Participantes.Add(new ParticipanteSessaoTreinamento { TrabalhadorId = joao.Id });
        db.SessoesTreinamento.Add(turma);
        await db.SaveChangesAsync();

        var plano = Assert.Single(await ListarAsync(db));

        var passo = plano.Passos.Single(p => p.Codigo == "nr06");
        Assert.Equal(SituacaoPasso.Agendado, passo.Situacao);
        Assert.Contains("22/10/2026", passo.Detalhe);
        Assert.Equal($"/treinamentos/turma/{turma.Id}", passo.Rota);
    }

    [Fact]
    public async Task TudoCumprido_PlanoSaiDaListaComoConcluido()
    {
        var (db, joao, integracao, nr6, nr35, bota) = await MontarAsync();
        db.Asos.Add(new Aso { TrabalhadorId = joao.Id, ResultadoStatus = ResultadoAso.Apto, DataExame = DateTime.UtcNow.Date, DataValidade = DateTime.UtcNow.Date.AddYears(1) });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = joao.Id, TemplateCriptografado = "x", CapturadoEm = DateTime.UtcNow });
        db.FotosCadastroFacial.Add(new FotoCadastroFacial { TrabalhadorId = joao.Id, Conteudo = new byte[] { 1 }, HashSha256 = "a", CapturadaEm = DateTime.UtcNow });
        db.Treinamentos.AddRange(Valido(joao, integracao), Valido(joao, nr6), Valido(joao, nr35));
        var reserva = await db.ReservasEpi.SingleAsync(r => r.CatalogoEpiId == bota.Id);
        reserva.Status = StatusReservaEpi.Entregue;
        await db.SaveChangesAsync();

        Assert.Empty(await ListarAsync(db));
        Assert.Equal(StatusPlanoIntegracao.Concluido, (await db.PlanosIntegracao.SingleAsync()).Status);
    }

    // Com o Entra ID ligado, quem não é Técnico de Segurança nem Administrador não recebe o pop-up.
    [Fact]
    public async Task UsuarioSemPerfilDeTecnico_NaoRecebePlanos()
    {
        var (db, _, _, _, _, _) = await MontarAsync();

        var planos = await new ListarPlanosIntegracaoPendentesQueryHandler(db)
            .Handle(new ListarPlanosIntegracaoPendentesQuery(null, "oid-de-encarregado", AutenticacaoHabilitada: true), default);

        Assert.Empty(planos);
    }
}
