using AAHBRANT.SST.Application.Calendario;
using AAHBRANT.SST.Application.Calendario.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Calendario;

public class ObterCalendarioQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<Usuario> SemearUsuarioAsync(IAppDbContext db, string azureAdObjectId)
    {
        var usuario = new Usuario { Nome = "Usuário Teste", Email = $"{Guid.NewGuid()}@teste.com", AzureAdObjectId = azureAdObjectId };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task Handle_SemAzureAdObjectId_RetornaUsuarioNaoIdentificado()
    {
        var db = CriarDb(nameof(Handle_SemAzureAdObjectId_RetornaUsuarioNaoIdentificado));
        var handler = new ObterCalendarioQueryHandler(db, new CalendarioTeamsServiceFalso(), NullLogger<ObterCalendarioQueryHandler>.Instance);

        var resultado = await handler.Handle(new ObterCalendarioQuery(null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7)), default);

        Assert.False(resultado.UsuarioIdentificado);
        Assert.False(resultado.GraphDisponivel);
        Assert.Empty(resultado.EventosGraph);
        Assert.Empty(resultado.EventosSst);
    }

    [Fact]
    public async Task Handle_UsuarioIdentificadoComSucessoNoGraph_CombinaEventosSstEGraph()
    {
        var db = CriarDb(nameof(Handle_UsuarioIdentificadoComSucessoNoGraph_CombinaEventosSstEGraph));
        var azureAdObjectId = Guid.NewGuid().ToString();
        var usuario = await SemearUsuarioAsync(db, azureAdObjectId);

        var inicio = new DateTime(2026, 9, 1);
        var fim = new DateTime(2026, 9, 30);
        db.Alertas.Add(new Alerta
        {
            Titulo = "ASO vencendo",
            Tipo = TipoAlerta.AsoVencendo,
            Severidade = SeveridadeAlerta.Atencao,
            Status = StatusAlerta.Aberto,
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = Guid.NewGuid(),
            DestinatarioUsuarioId = usuario.Id,
            DataLimiteTratamento = new DateTime(2026, 9, 15),
        });
        await db.SaveChangesAsync();

        var graphFalso = new CalendarioTeamsServiceFalso
        {
            EventosARetornar = { new EventoGraphDto("evt-1", "Reunião de obra", inicio.AddDays(2), inicio.AddDays(2).AddHours(1), false, "Sala 1", "Fulano", false, null) },
        };
        var handler = new ObterCalendarioQueryHandler(db, graphFalso, NullLogger<ObterCalendarioQueryHandler>.Instance);

        var resultado = await handler.Handle(new ObterCalendarioQuery(azureAdObjectId, inicio, fim), default);

        Assert.True(resultado.UsuarioIdentificado);
        Assert.True(resultado.GraphDisponivel);
        Assert.Null(resultado.MensagemErroGraph);
        Assert.Single(resultado.EventosGraph);
        Assert.Single(resultado.EventosSst);
        Assert.Equal("ASO vencendo", resultado.EventosSst[0].Titulo);
    }

    [Fact]
    public async Task Handle_GraphFalha_MantemEventosSstEDegradaComMensagemDeErro()
    {
        var db = CriarDb(nameof(Handle_GraphFalha_MantemEventosSstEDegradaComMensagemDeErro));
        var azureAdObjectId = Guid.NewGuid().ToString();
        var usuario = await SemearUsuarioAsync(db, azureAdObjectId);

        var inicio = new DateTime(2026, 9, 1);
        var fim = new DateTime(2026, 9, 30);
        db.Alertas.Add(new Alerta
        {
            Titulo = "Treinamento vencido",
            Tipo = TipoAlerta.TreinamentoVencido,
            Severidade = SeveridadeAlerta.Critico,
            Status = StatusAlerta.Aberto,
            EntidadeOrigemTipo = "Treinamento",
            EntidadeOrigemId = Guid.NewGuid(),
            DestinatarioUsuarioId = usuario.Id,
            DataLimiteTratamento = new DateTime(2026, 9, 10),
        });
        await db.SaveChangesAsync();

        var handler = new ObterCalendarioQueryHandler(db, new GraphFalhandoSempre(), NullLogger<ObterCalendarioQueryHandler>.Instance);
        var resultado = await handler.Handle(new ObterCalendarioQuery(azureAdObjectId, inicio, fim), default);

        Assert.True(resultado.UsuarioIdentificado);
        Assert.False(resultado.GraphDisponivel);
        Assert.NotNull(resultado.MensagemErroGraph);
        Assert.Single(resultado.EventosSst);
        Assert.Empty(resultado.EventosGraph);
    }

    // Helper mínimo para simular ListarEventosAsync lançando exceção (o CalendarioTeamsServiceFalso
    // compartilhado não tem esse hook — não vale complicá-lo só por este teste).
    private class GraphFalhandoSempre : ICalendarioTeamsService
    {
        public Task<string> CriarEventoAsync(Guid organizadorUsuarioId, string titulo, string? descricao, DateTime data, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task AtualizarEventoAsync(Guid organizadorUsuarioId, string graphEventId, string titulo, string? descricao, DateTime data, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CancelarEventoAsync(Guid organizadorUsuarioId, string graphEventId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<EventoGraphDto>> ListarEventosAsync(Guid usuarioId, DateTime inicio, DateTime fim, CancellationToken ct = default) =>
            throw new InvalidOperationException("Graph:ClientSecret não configurado — permissão Calendars.ReadWrite do Entra ID ainda não provisionada.");
    }
}
