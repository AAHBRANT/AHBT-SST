using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class CadastrosFaciaisFracosTests
{
    private static readonly DateTime Agora = DateTime.UtcNow;

    private static async Task<(SstDbContext Db, Obra Obra, Trabalhador Ana)> CriarAsync(DateTime cadastroEm)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var ana = new Trabalhador { Nome = "Ana", Matricula = "31", Obra = obra, ObraId = obra.Id };
        db.AddRange(obra, ana);
        db.FotosCadastroFacial.Add(new FotoCadastroFacial
        {
            TrabalhadorId = ana.Id, Conteudo = new byte[] { 1 }, ContentType = "image/jpeg",
            HashSha256 = new string('a', 64), CapturadaEm = cadastroEm,
        });
        await db.SaveChangesAsync();
        return (db, obra, ana);
    }

    private static void Falhas(SstDbContext db, Obra obra, Guid? trabalhadorId, int quantas, DateTime aPartirDe, MotivoFalhaFacial motivo = MotivoFalhaFacial.ConfiancaBaixa)
    {
        for (var i = 0; i < quantas; i++)
            db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial
            {
                ObraId = obra.Id, TrabalhadorId = trabalhadorId, Motivo = motivo, Confianca = 0.7, OcorridaEm = aPartirDe.AddMinutes(i),
            });
    }

    private static Task<List<CadastroFacialFracoDto>> Listar(SstDbContext db, Guid? obraId = null) =>
        new ListarCadastrosFaciaisFracosQueryHandler(db).Handle(new ListarCadastrosFaciaisFracosQuery(obraId), CancellationToken.None);

    [Fact]
    public async Task TresFalhasDepoisDoCadastro_AparecemNaLista()
    {
        var (db, obra, ana) = await CriarAsync(Agora.AddDays(-10));
        Falhas(db, obra, ana.Id, 3, Agora.AddDays(-2));
        await db.SaveChangesAsync();

        var lista = await Listar(db);

        var item = Assert.Single(lista);
        Assert.Equal("Ana", item.Nome);
        Assert.Equal("31", item.Matricula);
        Assert.Equal("Obra Sul", item.ObraNome);
        Assert.Equal(3, item.Falhas);
        Assert.Equal("Baixa confiança", item.UltimoMotivo);
    }

    [Fact]
    public async Task DuasFalhas_NaoAparecem()
    {
        var (db, obra, ana) = await CriarAsync(Agora.AddDays(-10));
        Falhas(db, obra, ana.Id, 2, Agora.AddDays(-2));
        await db.SaveChangesAsync();

        Assert.Empty(await Listar(db));
    }

    [Fact]
    public async Task FalhasAntesDoCadastroAtual_NaoContam_QuemRefezSaiDaLista()
    {
        var (db, obra, ana) = await CriarAsync(Agora.AddDays(-1));
        Falhas(db, obra, ana.Id, 5, Agora.AddDays(-5)); // todas antes de refazer o cadastro
        await db.SaveChangesAsync();

        Assert.Empty(await Listar(db));
    }

    [Fact]
    public async Task FalhasComMaisDe30Dias_NaoContam()
    {
        var (db, obra, ana) = await CriarAsync(Agora.AddDays(-90));
        Falhas(db, obra, ana.Id, 4, Agora.AddDays(-40));
        await db.SaveChangesAsync();

        Assert.Empty(await Listar(db));
    }

    [Fact]
    public async Task FalhasSemTrabalhadorAtribuido_SaoIgnoradas()
    {
        var (db, obra, _) = await CriarAsync(Agora.AddDays(-10));
        Falhas(db, obra, null, 6, Agora.AddDays(-1), MotivoFalhaFacial.RostoNaoReconhecido);
        await db.SaveChangesAsync();

        Assert.Empty(await Listar(db));
    }

    [Fact]
    public async Task OrdenaPorMaisFalhas_EFiltraPorObra()
    {
        var (db, obra, ana) = await CriarAsync(Agora.AddDays(-10));
        var bruno = new Trabalhador { Nome = "Bruno", Matricula = "16", Obra = obra, ObraId = obra.Id };
        db.Add(bruno);
        db.FotosCadastroFacial.Add(new FotoCadastroFacial
        {
            TrabalhadorId = bruno.Id, Conteudo = new byte[] { 1 }, ContentType = "image/jpeg",
            HashSha256 = new string('b', 64), CapturadaEm = Agora.AddDays(-10),
        });
        Falhas(db, obra, ana.Id, 3, Agora.AddDays(-2));
        Falhas(db, obra, bruno.Id, 7, Agora.AddDays(-2));
        await db.SaveChangesAsync();

        var todas = await Listar(db);
        Assert.Equal(new[] { "Bruno", "Ana" }, todas.Select(i => i.Nome).ToArray());
        Assert.Empty(await Listar(db, Guid.NewGuid())); // outra obra: nada
    }
}
