using AAHBRANT.SST.Application.Equipes.Queries;
using AAHBRANT.SST.Application.Setores.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Equipes;

// "Excluir" neste sistema é soft delete (Ativo=false) e quase toda entidade tem filtro global por
// Ativo. Projetar o nome do pai por navegação obrigatória (`e.Setor!.Nome`) faz o EF gerar INNER
// JOIN, e o filtro do pai apaga a linha filha inteira do resultado — foi o que derrubou a entrega
// de EPI em 23/09, via a lista de certificados.
//
// Aqui o filho é a estrutura organizacional da obra: setor e equipe continuam com trabalhadores
// vinculados depois que a obra ou o setor sai. Sumirem da tela significa não ter como reatribuir
// ninguém. Estes testes travam isso.
public class ListagemOrganizacionalComPaiExcluidoTests
{
    private static async Task<(Obra obra, Setor setor, Equipe equipe)> SemearAsync(SstDbContext db)
    {
        var obra = new Obra { Nome = "Edifício Aurora", Codigo = "AUR" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        var setor = new Setor { ObraId = obra.Id, Nome = "Estrutura" };
        db.Setores.Add(setor);
        await db.SaveChangesAsync();

        var equipe = new Equipe { SetorId = setor.Id, Nome = "Equipe A" };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync();
        return (obra, setor, equipe);
    }

    [Fact]
    public async Task ListarSetores_ObraExcluida_SetorContinuaNaLista()
    {
        var db = DbContextFactory.Criar();
        var (obra, _, _) = await SemearAsync(db);
        obra.Ativo = false; // estado do banco depois de excluir a obra
        await db.SaveChangesAsync();
        var handler = new ListarSetoresQueryHandler(db);

        var setores = await handler.Handle(new ListarSetoresQuery(null), default);

        var setor = Assert.Single(setores);
        Assert.Equal("Estrutura", setor.Nome);
        // Nome real + marca: quem olha a tela reconhece a obra e vê que ela saiu.
        Assert.Equal("Edifício Aurora (obra removida)", setor.ObraNome);
    }

    [Fact]
    public async Task ListarEquipes_SetorExcluido_EquipeContinuaNaLista()
    {
        var db = DbContextFactory.Criar();
        var (_, setor, _) = await SemearAsync(db);
        setor.Ativo = false;
        await db.SaveChangesAsync();
        var handler = new ListarEquipesQueryHandler(db);

        var equipes = await handler.Handle(new ListarEquipesQuery(null, null), default);

        var equipe = Assert.Single(equipes);
        Assert.Equal("Equipe A", equipe.Nome);
        Assert.Equal("Estrutura (setor removido)", equipe.SetorNome);
    }

    // O pior dos casos: sem isto, abrir a equipe devolvia null e a tela dava 404 — a equipe existia
    // no banco, com gente dentro, e simplesmente não abria mais.
    [Fact]
    public async Task ObterEquipePorId_SetorExcluido_AindaAbreAEquipe()
    {
        var db = DbContextFactory.Criar();
        var (_, setor, equipe) = await SemearAsync(db);
        setor.Ativo = false;
        await db.SaveChangesAsync();
        var handler = new ObterEquipePorIdQueryHandler(db);

        var resultado = await handler.Handle(new ObterEquipePorIdQuery(equipe.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal("Equipe A", resultado!.Nome);
        Assert.Equal("Estrutura (setor removido)", resultado.SetorNome);
    }

    [Fact]
    public async Task ListagensComPaiAtivo_ContinuamMostrandoONomeDoPai()
    {
        var db = DbContextFactory.Criar();
        var (_, _, equipe) = await SemearAsync(db);
        var handlerSetores = new ListarSetoresQueryHandler(db);
        var handlerEquipes = new ListarEquipesQueryHandler(db);
        var handlerEquipe = new ObterEquipePorIdQueryHandler(db);

        var setores = await handlerSetores.Handle(new ListarSetoresQuery(null), default);
        var equipes = await handlerEquipes.Handle(new ListarEquipesQuery(null, null), default);
        var uma = await handlerEquipe.Handle(new ObterEquipePorIdQuery(equipe.Id), default);

        Assert.Equal("Edifício Aurora", Assert.Single(setores).ObraNome);
        Assert.Equal("Estrutura", Assert.Single(equipes).SetorNome);
        Assert.Equal("Edifício Aurora", Assert.Single(equipes).ObraNome);
        Assert.Equal("Estrutura", uma!.SetorNome);
    }
}
