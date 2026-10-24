using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

// Brasília é UTC-3: 10:02Z = 07:02. A duração do DDS vai da 1ª à última assinatura de participante.
public class ConstruirListaPresencaTests
{
    private sealed record Cenario(SstDbContext Db, DdsEntidade Dds, Obra Obra, Trabalhador Ana, Trabalhador Bruno, Trabalhador Carla);

    private static readonly DateTime Dia = new(2026, 10, 7);

    private static async Task<Cenario> CriarAsync(DateTime? encerradoEm = null)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var usuario = new Usuario { Nome = "Responsável", Email = "r@example.test" };
        var dds = new DdsEntidade
        {
            Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id,
            Data = Dia, Status = StatusDds.Concluido, EncerradoEm = encerradoEm,
        };
        var ana = new Trabalhador { Nome = "Ana", Matricula = "101", Obra = obra, ObraId = obra.Id };
        var bruno = new Trabalhador { Nome = "Bruno", Matricula = "102", Obra = obra, ObraId = obra.Id };
        var carla = new Trabalhador { Nome = "Carla", Matricula = "103", Obra = obra, ObraId = obra.Id };
        db.AddRange(obra, usuario, dds, ana, bruno, carla);
        await db.SaveChangesAsync();
        return new Cenario(db, dds, obra, ana, bruno, carla);
    }

    private static DdsDetalheDto Detalhe(Cenario c, IEnumerable<(Trabalhador T, DateTime? Assinou)> presentes, IEnumerable<Trabalhador> ausentes, string? temaLivre = null) => new()
    {
        Dds = new DdsDto { Id = c.Dds.Id, ObraId = c.Obra.Id, ObraNome = c.Obra.Nome, Data = Dia, TemaLivreNome = temaLivre },
        Participantes = presentes.Select(p => new DdsParticipanteDto { TrabalhadorId = p.T.Id, TrabalhadorNome = p.T.Nome, AssinadoEm = p.Assinou }).ToList(),
        FuncionariosSelecionados = ausentes.Select(a => new DdsFuncionarioDto(a.Id, a.Nome, a.Matricula)).ToList(),
    };

    private static ConstruirListaPresencaQueryHandler Handler(Cenario c, DdsDetalheDto detalhe) =>
        new(new MediatorSimulado(_ => detalhe), c.Db);

    [Fact]
    public async Task Duracao_VaiDaPrimeiraAUltimaAssinatura_EmHorarioDeBrasilia()
    {
        var c = await CriarAsync(encerradoEm: new DateTime(2026, 10, 7, 11, 10, 0, DateTimeKind.Utc));
        var detalhe = Detalhe(c, new[]
        {
            (c.Ana, (DateTime?)new DateTime(2026, 10, 7, 10, 41, 0, DateTimeKind.Utc)),
            (c.Bruno, new DateTime(2026, 10, 7, 10, 2, 0, DateTimeKind.Utc)),
        }, new[] { c.Carla });

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Equal(39, d.DuracaoMinutos);
        Assert.Equal(new DateTime(2026, 10, 7, 7, 2, 0), d.PrimeiraAssinatura);
        Assert.Equal(new DateTime(2026, 10, 7, 7, 41, 0), d.UltimaAssinatura);
        Assert.Equal(new DateTime(2026, 10, 7, 8, 10, 0), d.FechadoEm);
        Assert.Equal(68, d.MinutosAteFechar); // da 1ª assinatura ao fechamento
        Assert.Equal((2, 3, 1), (d.QuantidadePresentes, d.Total, d.Ausentes.Count));
    }

    [Fact]
    public async Task ComUmaSoAssinatura_NaoHaComoCalcularADuracao()
    {
        var c = await CriarAsync();
        var detalhe = Detalhe(c, new[] { (c.Ana, (DateTime?)new DateTime(2026, 10, 7, 10, 2, 0, DateTimeKind.Utc)) }, Array.Empty<Trabalhador>());

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Null(d.DuracaoMinutos);
        Assert.Equal(new DateTime(2026, 10, 7, 7, 2, 0), d.PrimeiraAssinatura);
        Assert.Null(d.FechadoEm); // DDS antigo, sem o registro de fechamento
        Assert.Null(d.MinutosAteFechar);
    }

    [Fact]
    public async Task PresencaSemAssinatura_NaoEntraNaDuracao()
    {
        var c = await CriarAsync();
        var detalhe = Detalhe(c, new[] { (c.Ana, (DateTime?)null), (c.Bruno, null) }, Array.Empty<Trabalhador>());

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Null(d.PrimeiraAssinatura);
        Assert.Null(d.DuracaoMinutos);
        Assert.All(d.Presentes, p => Assert.Null(p.AssinadoEm));
    }

    [Fact]
    public async Task PresentesSaemEmOrdemDeAssinatura_ComMatricula()
    {
        var c = await CriarAsync();
        var detalhe = Detalhe(c, new[]
        {
            (c.Carla, (DateTime?)new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc)),
            (c.Ana, new DateTime(2026, 10, 7, 10, 2, 0, DateTimeKind.Utc)),
            (c.Bruno, new DateTime(2026, 10, 7, 10, 15, 0, DateTimeKind.Utc)),
        }, Array.Empty<Trabalhador>());

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Equal(new[] { "Ana", "Bruno", "Carla" }, d.Presentes.Select(p => p.Nome));
        Assert.Equal(new[] { "101", "102", "103" }, d.Presentes.Select(p => p.Matricula));
    }

    [Fact]
    public async Task FaltaRepetida_ContaOsDdsSeguidosEmQueEstavaNaListaESemPresenca()
    {
        var c = await CriarAsync();
        // Dois DDS anteriores: Carla estava na lista e faltou nos dois; Bruno compareceu no mais recente.
        for (var i = 1; i <= 2; i++)
        {
            var anterior = new DdsEntidade
            {
                ObraId = c.Obra.Id, ResponsavelUsuarioId = c.Dds.ResponsavelUsuarioId, Data = Dia.AddDays(-i), Status = StatusDds.Concluido,
            };
            c.Db.Dds.Add(anterior);
            c.Db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = anterior.Id, TrabalhadorId = c.Carla.Id });
            c.Db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = anterior.Id, TrabalhadorId = c.Bruno.Id });
            if (i == 1) c.Db.DdsParticipantes.Add(new DdsParticipante { DdsId = anterior.Id, TrabalhadorId = c.Bruno.Id });
        }
        await c.Db.SaveChangesAsync();
        var detalhe = Detalhe(c, new[] { (c.Ana, (DateTime?)new DateTime(2026, 10, 7, 10, 2, 0, DateTimeKind.Utc)) }, new[] { c.Bruno, c.Carla });

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Equal(3, d.Ausentes.Single(a => a.Nome == "Carla").FaltasSeguidas); // hoje + 2 anteriores
        Assert.Equal(1, d.Ausentes.Single(a => a.Nome == "Bruno").FaltasSeguidas); // compareceu no DDS anterior
    }

    [Fact]
    public async Task DdsAnteriorSemExpedienteOuNaoEncerrado_NaoEntraNaContagem()
    {
        var c = await CriarAsync();
        var semExpediente = new DdsEntidade { ObraId = c.Obra.Id, ResponsavelUsuarioId = c.Dds.ResponsavelUsuarioId, Data = Dia.AddDays(-1), Status = StatusDds.Concluido, SemExpediente = true };
        var emAndamento = new DdsEntidade { ObraId = c.Obra.Id, ResponsavelUsuarioId = c.Dds.ResponsavelUsuarioId, Data = Dia.AddDays(-2), Status = StatusDds.EmAndamento };
        c.Db.AddRange(semExpediente, emAndamento);
        c.Db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = semExpediente.Id, TrabalhadorId = c.Carla.Id });
        c.Db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = emAndamento.Id, TrabalhadorId = c.Carla.Id });
        await c.Db.SaveChangesAsync();
        var detalhe = Detalhe(c, Array.Empty<(Trabalhador, DateTime?)>(), new[] { c.Carla });

        var d = (await Handler(c, detalhe).Handle(new ConstruirListaPresencaQuery(c.Dds.Id), CancellationToken.None))!;

        Assert.Equal(1, Assert.Single(d.Ausentes).FaltasSeguidas);
    }

    [Fact]
    public async Task DdsInexistente_DevolveNulo()
    {
        var c = await CriarAsync();
        var handler = new ConstruirListaPresencaQueryHandler(new MediatorSimulado(_ => null), c.Db);

        Assert.Null(await handler.Handle(new ConstruirListaPresencaQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
