using System.Net;
using System.Text.Json;
using AAHBRANT.SST.Application.NaoConformidades;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Integracao.GQuali;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Integracao.GQuali;

public class EnvioNcGQualiTests
{
    private static (Persistencia.SstDbContext db, Obra obra) Banco(string nome)
    {
        var db = SstDbContextInMemoryTests.CriarContexto(nome);
        var obra = new Obra { Codigo = "CAC-01", Nome = "Centro de Atividades" };
        db.Add(obra);
        db.SaveChanges();
        return (db, obra);
    }

    [Fact]
    public void NcComObra_GravaEventoCriadaNaMesmaGravacao()
    {
        var (db, obra) = Banco(nameof(NcComObra_GravaEventoCriadaNaMesmaGravacao));
        var nc = new NaoConformidade { Descricao = "Guarda-corpo ausente\nno 2º pavimento", ObraId = obra.Id, Local = "Bloco A" };
        db.NaoConformidades.Add(nc);
        db.SaveChanges();

        var evento = Assert.Single(db.EventosNcGQuali.AsNoTracking());
        Assert.Equal(EventoNcGQualiFactory.Criada, evento.Tipo);
        Assert.Equal(SituacaoEventoNcGQuali.Pendente, evento.Situacao);
        using var json = JsonDocument.Parse(evento.Payload);
        Assert.Equal("CAC-01", json.RootElement.GetProperty("obraCodigo").GetString());
        Assert.Equal("GSst", json.RootElement.GetProperty("sourceSystem").GetString());
        Assert.Equal("Guarda-corpo ausente", json.RootElement.GetProperty("titulo").GetString());
        Assert.False(json.RootElement.TryGetProperty("responsavelUsuarioId", out _));
    }

    [Fact]
    public void NcSemObra_NaoGeraEvento()
    {
        var (db, _) = Banco(nameof(NcSemObra_NaoGeraEvento));
        db.NaoConformidades.Add(new NaoConformidade { Descricao = "Antiga" });
        db.SaveChanges();
        Assert.Empty(db.EventosNcGQuali);
    }

    [Fact]
    public void Encerramento_GeraEventoEncerrada_ComRevisaoMaior()
    {
        var (db, obra) = Banco(nameof(Encerramento_GeraEventoEncerrada_ComRevisaoMaior));
        var nc = new NaoConformidade { Descricao = "Extintor vencido", ObraId = obra.Id };
        db.NaoConformidades.Add(nc);
        db.SaveChanges();
        nc.Status = StatusNaoConformidade.Encerrada;
        db.SaveChanges();

        var eventos = db.EventosNcGQuali.AsNoTracking().OrderBy(e => e.Revisao).ToList();
        Assert.Equal([EventoNcGQualiFactory.Criada, EventoNcGQualiFactory.Encerrada], eventos.Select(e => e.Tipo));
        Assert.True(eventos[1].Revisao > eventos[0].Revisao);
        Assert.NotEqual(eventos[0].EventId, eventos[1].EventId);
    }

    [Fact]
    public void Resposta400_RecusaSemRepetir()
    {
        var e = new EventoNcGQuali { Tentativas = 1 };
        EnvioNcGQualiService.Registrar(e, HttpStatusCode.BadRequest, "obraCodigo obrigatório");
        Assert.Equal(SituacaoEventoNcGQuali.Recusado, e.Situacao);
        Assert.Contains("400", e.UltimoErro);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void FalhaTemporaria_VoltaParaPendenteComEspera(HttpStatusCode status)
    {
        var e = new EventoNcGQuali { Tentativas = 3, Situacao = SituacaoEventoNcGQuali.Enviando };
        var antes = DateTime.UtcNow;
        EnvioNcGQualiService.Registrar(e, status, "x");
        Assert.Equal(SituacaoEventoNcGQuali.Pendente, e.Situacao);
        Assert.True(e.ProximaTentativaEmUtc >= antes.AddMinutes(4)); // 3ª tentativa → 4 min
    }

    [Fact]
    public void Espera_LimitadaAUmaHora()
    {
        var e = new EventoNcGQuali { Tentativas = 30 };
        EnvioNcGQualiService.Reagendar(e, new string('x', 5000));
        Assert.True(e.ProximaTentativaEmUtc <= DateTime.UtcNow.AddMinutes(61));
        Assert.Equal(1000, e.UltimoErro!.Length);
    }

    [Fact]
    public void Opcoes_SemSegredo_NaoConfigurada()
    {
        Assert.False(new GQualiOptions { BaseUrl = "https://x", TenantId = "t", ClientId = "c", Scope = "s" }.Configurada);
    }
}
