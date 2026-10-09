using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

// Reunião de análise obrigatória em todo acidente registrado por relato (09/10/2026).
public class ReuniaoAnaliseOcorrenciaTests
{
    [Fact]
    public void PrimeiroLivre_PulaOcupadoEAlmoco_IgnoraDiaInteiro()
    {
        var dia = new DateTime(2026, 10, 9);
        var eventos = new[]
        {
            (dia.AddHours(8), dia.AddHours(11).AddMinutes(30), false), // manhã toda ocupada
            (dia, dia.AddDays(1), true),                               // lembrete de dia inteiro: não ocupa
        };

        Assert.Equal(dia.AddHours(13), HorarioReuniao.PrimeiroLivre(dia, eventos)); // 12h é almoço
    }

    [Fact]
    public void PrimeiroLivre_AgendaCheia_DevolveNulo()
        => Assert.Null(HorarioReuniao.PrimeiroLivre(new DateTime(2026, 10, 9),
            new[] { (new DateTime(2026, 10, 9, 7, 0, 0), new DateTime(2026, 10, 9, 18, 0, 0), false) }));

    [Fact]
    public void Validador_AcidentePorRelatoSemReuniao_Recusa()
    {
        var comando = Comando(Guid.NewGuid(), TipoOcorrencia.Acidente) with
        {
            AcoesPlano = new List<AcaoPlanoNovaOcorrencia>(),
            TemaDds = new TemaDdsNovaOcorrencia("Tema", "Roteiro"),
        };
        Assert.Contains(new CriarAcidenteCommandValidator().Validate(comando).Errors, e => e.ErrorMessage.Contains("reunião de análise"));

        // Quase acidente não exige reunião.
        Assert.True(new CriarAcidenteCommandValidator().Validate(comando with { Tipo = TipoOcorrencia.QuaseAcidente }).IsValid);
    }

    [Theory]
    [InlineData(true, false, SituacaoReuniaoTeams.Criada)]
    [InlineData(true, true, SituacaoReuniaoTeams.NaoCriada)]
    [InlineData(false, false, SituacaoReuniaoTeams.NaoCriada)]
    public async Task Registro_CriaAcaoEReuniao_TeamsFalharNaoDerrubaORegistro(bool teamsConfigurado, bool teamsFalha, SituacaoReuniaoTeams esperado)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        var tecnico = new Usuario { Nome = "Técnico", Email = "tec@x.com", AzureAdObjectId = "aad-tec" };
        var engenheira = new Usuario { Nome = "Carla", Email = "carla@x.com" };
        db.AddRange(obra, tecnico, engenheira);
        await db.SaveChangesAsync();

        var teams = new ReuniaoTeamsFalsa(teamsConfigurado, teamsFalha);
        var inicio = new DateTime(2026, 10, 9, 10, 0, 0);
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorGrhFalso(), teams).Handle(
            Comando(obra.Id, TipoOcorrencia.Acidente) with
            {
                AcoesPlano = new List<AcaoPlanoNovaOcorrencia>(),
                UsuarioAtualId = tecnico.Id,
                Reuniao = new ReuniaoAnaliseNovaOcorrencia(inicio, 60, new List<Guid> { engenheira.Id }),
            }, default);

        Assert.True(await db.Acidentes.AnyAsync(a => a.Id == id)); // registro gravado mesmo com falha
        var reuniao = await db.ReunioesAnaliseOcorrencia.SingleAsync();
        var acao = await db.AcoesPlano.SingleAsync(a => a.Id == reuniao.AcaoPlanoId);
        Assert.Equal(esperado, reuniao.Situacao);
        Assert.Equal(inicio, acao.Prazo);
        Assert.Equal(tecnico.Id, acao.ResponsavelUsuarioId);
        Assert.Equal(inicio.AddHours(1), reuniao.Fim);
        Assert.Equal(new[] { "Técnico", "Carla" }, reuniao.Participantes.Split('\n'));
        if (esperado == SituacaoReuniaoTeams.Criada)
        {
            Assert.Equal("https://teams/link", reuniao.LinkTeams);
            Assert.Equal(new[] { "tec@x.com", "carla@x.com" }, teams.Convidados.OrderByDescending(e => e.StartsWith("tec")));
        }
        else
        {
            Assert.NotNull(reuniao.MotivoFalha);
        }
    }

    private static CriarAcidenteCommand Comando(Guid obraId, TipoOcorrencia tipo) => new(
        tipo, obraId, null, null, "Bloco B", new DateTime(2026, 10, 8), null, "Queda de escada",
        null, null, null, false, null, null, null, null, GravidadeAcidente.SemAfastamento, null);

    private sealed class PublicadorGrhFalso : IPublicadorAcidenteGrh
    {
        public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ReuniaoTeamsFalsa : IReuniaoTeamsService
    {
        private readonly bool _falha;
        public ReuniaoTeamsFalsa(bool configurado, bool falha) { Configurado = configurado; _falha = falha; }
        public bool Configurado { get; }
        public List<string> Convidados { get; } = new();

        public Task<ReuniaoTeamsCriada> CriarReuniaoAsync(Guid organizadorUsuarioId, string titulo, string descricao,
            DateTime inicio, DateTime fim, IReadOnlyList<string> emailsConvidados, CancellationToken ct = default)
        {
            if (_falha) throw new InvalidOperationException("Graph 403");
            Convidados.AddRange(emailsConvidados);
            return Task.FromResult(new ReuniaoTeamsCriada("evt-1", "https://teams/link"));
        }
    }
}
