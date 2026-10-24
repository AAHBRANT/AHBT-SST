using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

// Mapa corporal da lesão (24/10/2026): partes do corpo atingidas gravadas com a ocorrência.
public class RegioesCorpoAcidenteTests
{
    [Fact]
    public void Validador_RecusaCodigoForaDoFormato()
    {
        var comando = Comando(Guid.NewGuid()) with { RegioesCorpo = new() { "F:mao-esq", "mão esquerda" } };
        var resultado = new CriarAcidenteCommandValidator().Validate(comando);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("Parte do corpo inválida"));
    }

    [Fact]
    public async Task Registro_GravaSemRepetirEDetalheDevolveALista()
    {
        var (db, obra) = await SemearAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorGrhFalso()).Handle(
            Comando(obra.Id) with { RegioesCorpo = new() { "F:mao-esq", "C:lombar-dir", "F:mao-esq" } }, default);

        Assert.Equal("F:mao-esq;C:lombar-dir", (await db.Acidentes.SingleAsync()).RegioesCorpo);
        var detalhe = await new ObterAcidenteDetalheQueryHandler(db).Handle(new ObterAcidenteDetalheQuery(id), default);
        Assert.Equal(new[] { "F:mao-esq", "C:lombar-dir" }, detalhe.Acidente.RegioesCorpo);
    }

    // A tela de investigação regrava a ocorrência inteira sem mandar o mapa: não pode apagá-lo.
    [Fact]
    public async Task Atualizacao_SemRegioesMantem_ComListaVaziaLimpa()
    {
        var (db, obra) = await SemearAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorGrhFalso()).Handle(
            Comando(obra.Id) with { RegioesCorpo = new() { "F:joelho-dir" } }, default);
        var atualizar = new AtualizarAcidenteCommandHandler(db, new PublicadorGrhFalso());

        await atualizar.Handle(Atualizacao(id, obra.Id, null), default);
        Assert.Equal("F:joelho-dir", (await db.Acidentes.SingleAsync()).RegioesCorpo);

        await atualizar.Handle(Atualizacao(id, obra.Id, new List<string>()), default);
        Assert.Null((await db.Acidentes.SingleAsync()).RegioesCorpo);
    }

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Obra Obra)> SemearAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        db.Add(obra);
        await db.SaveChangesAsync();
        return (db, obra);
    }

    private static CriarAcidenteCommand Comando(Guid obraId) => new(
        TipoOcorrencia.Acidente, obraId, null, null, "Bloco B", new DateTime(2026, 10, 8), null, "Queda de escada",
        "Entorse", null, null, false, null, null, null, null, GravidadeAcidente.SemAfastamento, null);

    private static AtualizarAcidenteCommand Atualizacao(Guid id, Guid obraId, List<string>? regioes) => new(
        id, TipoOcorrencia.Acidente, obraId, null, null, "Bloco B", new DateTime(2026, 10, 8), null, "Queda de escada",
        "Entorse", null, null, false, null, null, null, "Causas", GravidadeAcidente.SemAfastamento, null, null, regioes);

    private sealed class PublicadorGrhFalso : IPublicadorAcidenteGrh
    {
        public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default) => Task.CompletedTask;
    }
}
