using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests;

public class FotosCadastroFacialQueriesTests
{
    private static IAppDbContext CriarDb(string nome)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static FotoCadastroFacial Foto(Guid trabalhadorId, DateTime quando, byte marca) => new()
    {
        TrabalhadorId = trabalhadorId,
        Conteudo = new[] { marca },
        ContentType = "image/jpeg",
        HashSha256 = new string('a', 64),
        CapturadaEm = quando,
    };

    [Fact]
    public async Task Listar_DevolveSoDoTrabalhadorDaMaisRecenteParaAMaisAntiga()
    {
        var db = CriarDb(nameof(Listar_DevolveSoDoTrabalhadorDaMaisRecenteParaAMaisAntiga));
        var dono = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var antiga = Foto(dono, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), 1);
        var recente = Foto(dono, new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc), 2);
        db.FotosCadastroFacial.AddRange(antiga, recente, Foto(outro, DateTime.UtcNow, 3));
        await db.SaveChangesAsync();

        var lista = await new ListarFotosCadastroFacialQueryHandler(db)
            .Handle(new ListarFotosCadastroFacialQuery(dono), CancellationToken.None);

        Assert.Equal(new[] { recente.Id, antiga.Id }, lista.Select(f => f.Id));
    }

    [Fact]
    public async Task Obter_DevolveBytesDaFotoDoProprioTrabalhador()
    {
        var db = CriarDb(nameof(Obter_DevolveBytesDaFotoDoProprioTrabalhador));
        var dono = Guid.NewGuid();
        var foto = Foto(dono, DateTime.UtcNow, 7);
        db.FotosCadastroFacial.Add(foto);
        await db.SaveChangesAsync();

        var resultado = await new ObterFotoCadastroFacialQueryHandler(db)
            .Handle(new ObterFotoCadastroFacialQuery(dono, foto.Id), CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(new byte[] { 7 }, resultado!.Conteudo);
        Assert.Equal("image/jpeg", resultado.ContentType);
    }

    // A foto só sai pelo perfil do dono: pedir a foto de A pelo id do trabalhador B não devolve nada.
    [Fact]
    public async Task Obter_FotoDeOutroTrabalhador_NaoDevolve()
    {
        var db = CriarDb(nameof(Obter_FotoDeOutroTrabalhador_NaoDevolve));
        var foto = Foto(Guid.NewGuid(), DateTime.UtcNow, 9);
        db.FotosCadastroFacial.Add(foto);
        await db.SaveChangesAsync();

        var resultado = await new ObterFotoCadastroFacialQueryHandler(db)
            .Handle(new ObterFotoCadastroFacialQuery(Guid.NewGuid(), foto.Id), CancellationToken.None);

        Assert.Null(resultado);
    }
}
