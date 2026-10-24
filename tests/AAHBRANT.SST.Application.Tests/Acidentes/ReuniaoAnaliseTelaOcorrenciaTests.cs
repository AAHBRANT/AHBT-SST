using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

// Reunião de análise pela tela da ocorrência (23/10/2026): agendar em qualquer tipo, remarcar,
// tentar de novo no Teams, registrar realizada com ata e trava na finalização da investigação.
public class ReuniaoAnaliseTelaOcorrenciaTests
{
    private static readonly DateTime Inicio = new(2026, 10, 24, 14, 0, 0);

    [Fact]
    public async Task Agendar_QuaseAcidente_CriaReuniaoAcaoENoTeams()
    {
        var (db, acidente, tecnico, engenheira) = await CenarioAsync(TipoOcorrencia.QuaseAcidente);
        var teams = new TeamsFalso();

        await new AgendarReuniaoAnaliseCommandHandler(db, teams).Handle(
            new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid> { engenheira.Id }, tecnico.Id), default);

        var reuniao = await db.ReunioesAnaliseOcorrencia.SingleAsync();
        var acao = await db.AcoesPlano.SingleAsync(a => a.Id == reuniao.AcaoPlanoId);
        Assert.Equal(SituacaoReuniaoTeams.Criada, reuniao.Situacao);
        Assert.Equal(Inicio, acao.Prazo);
        Assert.Equal(Inicio.AddHours(1), reuniao.Fim);
        Assert.Contains("Análise de quase acidente", teams.Titulos.Single());
        Assert.Equal(2, reuniao.ParticipantesUsuarioIds!.Split('\n').Length);
    }

    [Fact]
    public async Task Agendar_DuasVezes_Recusa()
    {
        var (db, acidente, tecnico, _) = await CenarioAsync(TipoOcorrencia.Incidente);
        var handler = new AgendarReuniaoAnaliseCommandHandler(db, new TeamsFalso());
        await handler.Handle(new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid>(), tecnico.Id), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid>(), tecnico.Id), default));
    }

    [Fact]
    public async Task Remarcar_NoTeams_MudaHorarioPrazoEAtualizaEvento()
    {
        var (db, acidente, tecnico, _) = await CenarioAsync(TipoOcorrencia.QuaseAcidente);
        var teams = new TeamsFalso();
        await new AgendarReuniaoAnaliseCommandHandler(db, teams).Handle(
            new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid>(), tecnico.Id), default);

        var novo = Inicio.AddDays(1);
        await new RemarcarReuniaoAnaliseCommandHandler(db, teams).Handle(new RemarcarReuniaoAnaliseCommand(acidente.Id, novo, 90), default);

        var reuniao = await db.ReunioesAnaliseOcorrencia.SingleAsync();
        Assert.Equal(novo, reuniao.Inicio);
        Assert.Equal(novo.AddMinutes(90), reuniao.Fim);
        Assert.Equal(novo, (await db.AcoesPlano.SingleAsync(a => a.Id == reuniao.AcaoPlanoId)).Prazo);
        Assert.Equal((novo, novo.AddMinutes(90)), teams.Remarcacoes.Single());
        Assert.Null(reuniao.MotivoFalha);
    }

    [Fact]
    public async Task TentarDeNovo_DepoisDeFalha_CriaNoTeamsComOsMesmosConvidados()
    {
        var (db, acidente, tecnico, engenheira) = await CenarioAsync(TipoOcorrencia.QuaseAcidente);
        await new AgendarReuniaoAnaliseCommandHandler(db, new TeamsFalso(falha: true)).Handle(
            new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid> { engenheira.Id }, tecnico.Id), default);
        Assert.Equal(SituacaoReuniaoTeams.NaoCriada, (await db.ReunioesAnaliseOcorrencia.SingleAsync()).Situacao);

        var teams = new TeamsFalso();
        await new TentarReuniaoAnaliseNoTeamsCommandHandler(db, teams).Handle(new TentarReuniaoAnaliseNoTeamsCommand(acidente.Id), default);

        var reuniao = await db.ReunioesAnaliseOcorrencia.SingleAsync();
        Assert.Equal(SituacaoReuniaoTeams.Criada, reuniao.Situacao);
        Assert.Null(reuniao.MotivoFalha);
        Assert.Equal(new[] { "carla@x.com", "tec@x.com" }, teams.Convidados.OrderBy(e => e));
    }

    [Fact]
    public async Task RegistrarRealizada_GravaAtaEConcluiAcao_LiberaFinalizacao()
    {
        var (db, acidente, tecnico, _) = await CenarioAsync(TipoOcorrencia.QuaseAcidente);
        acidente.Status = StatusAcidente.EmInvestigacao;
        await db.SaveChangesAsync();
        await new AgendarReuniaoAnaliseCommandHandler(db, new TeamsFalso()).Handle(
            new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid>(), tecnico.Id), default);

        var avancar = new AvancarStatusAcidenteCommandHandler(db, new PublicadorGrhFalso());
        var recusa = await Assert.ThrowsAsync<InvalidOperationException>(() => avancar.Handle(new AvancarStatusAcidenteCommand(acidente.Id), default));
        Assert.Contains("reunião de análise", recusa.Message);

        await new RegistrarReuniaoAnaliseRealizadaCommandHandler(db).Handle(
            new RegistrarReuniaoAnaliseRealizadaCommand(acidente.Id, Inicio, new List<string> { "Técnico", " ", "Carla" }, "  Causas confirmadas.  "), default);

        var reuniao = await db.ReunioesAnaliseOcorrencia.SingleAsync();
        Assert.Equal(Inicio, reuniao.RealizadaEm);
        Assert.Equal("Técnico\nCarla", reuniao.Presentes);
        Assert.Equal("Causas confirmadas.", reuniao.Ata);
        Assert.Equal(StatusControleRisco.Concluido, (await db.AcoesPlano.SingleAsync(a => a.Id == reuniao.AcaoPlanoId)).Status);

        await avancar.Handle(new AvancarStatusAcidenteCommand(acidente.Id), default);
        Assert.Equal(StatusAcidente.Concluido, (await db.Acidentes.SingleAsync()).Status);
    }

    [Fact]
    public async Task Remarcar_DepoisDeRealizada_Recusa()
    {
        var (db, acidente, tecnico, _) = await CenarioAsync(TipoOcorrencia.QuaseAcidente);
        await new AgendarReuniaoAnaliseCommandHandler(db, new TeamsFalso()).Handle(
            new AgendarReuniaoAnaliseCommand(acidente.Id, Inicio, 60, new List<Guid>(), tecnico.Id), default);
        await new RegistrarReuniaoAnaliseRealizadaCommandHandler(db).Handle(
            new RegistrarReuniaoAnaliseRealizadaCommand(acidente.Id, Inicio, new List<string> { "Técnico" }, "Ata"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemarcarReuniaoAnaliseCommandHandler(db, new TeamsFalso()).Handle(new RemarcarReuniaoAnaliseCommand(acidente.Id, Inicio.AddDays(1), 60), default));
    }

    [Fact]
    public void Validador_RealizadaSemAtaOuSemPresentes_Recusa()
    {
        var validador = new RegistrarReuniaoAnaliseRealizadaCommandValidator();
        Assert.False(validador.Validate(new RegistrarReuniaoAnaliseRealizadaCommand(Guid.NewGuid(), Inicio, new List<string> { "A" }, "")).IsValid);
        Assert.False(validador.Validate(new RegistrarReuniaoAnaliseRealizadaCommand(Guid.NewGuid(), Inicio, new List<string> { " " }, "Ata")).IsValid);
        Assert.True(validador.Validate(new RegistrarReuniaoAnaliseRealizadaCommand(Guid.NewGuid(), Inicio, new List<string> { "A" }, "Ata")).IsValid);
    }

    private static async Task<(IAppDbContext Db, Acidente Acidente, Usuario Tecnico, Usuario Engenheira)> CenarioAsync(TipoOcorrencia tipo)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var tecnico = new Usuario { Nome = "Técnico", Email = "tec@x.com", AzureAdObjectId = "aad-tec" };
        var engenheira = new Usuario { Nome = "Carla", Email = "carla@x.com" };
        var acidente = new Acidente { Tipo = tipo, ObraId = obra.Id, Local = "Bloco B", Data = new DateTime(2026, 10, 21), Descricao = "Peça caiu" };
        db.AddRange(obra, tecnico, engenheira, acidente);
        await db.SaveChangesAsync();
        return (db, acidente, tecnico, engenheira);
    }

    private sealed class PublicadorGrhFalso : IPublicadorAcidenteGrh
    {
        public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TeamsFalso : IReuniaoTeamsService
    {
        private readonly bool _falha;
        public TeamsFalso(bool falha = false) => _falha = falha;
        public bool Configurado => true;
        public List<string> Convidados { get; } = new();
        public List<string> Titulos { get; } = new();
        public List<(DateTime, DateTime)> Remarcacoes { get; } = new();

        public Task<ReuniaoTeamsCriada> CriarReuniaoAsync(Guid organizadorUsuarioId, string titulo, string descricao,
            DateTime inicio, DateTime fim, IReadOnlyList<string> emailsConvidados, CancellationToken ct = default)
        {
            if (_falha) throw new InvalidOperationException("Graph 403");
            Titulos.Add(titulo);
            Convidados.AddRange(emailsConvidados);
            return Task.FromResult(new ReuniaoTeamsCriada("evt-1", "https://teams/link"));
        }

        public Task RemarcarReuniaoAsync(Guid organizadorUsuarioId, string graphEventId, DateTime inicio, DateTime fim, CancellationToken ct = default)
        {
            Remarcacoes.Add((inicio, fim));
            return Task.CompletedTask;
        }
    }
}
