using AAHBRANT.SST.Application.ExamesFuncaoObra;
using AAHBRANT.SST.Application.Ghes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Ghes;

// Regressão de 10/10/2026: IgnoreQueryFilters usado dentro da projeção (para o nome da função) desligava
// os filtros da consulta inteira — GHE desativado voltava e usuário de outra obra via a estrutura.
public class ConsultasEstruturaFiltrosTests
{
    private static SstDbContext CriarDb(string nome, CurrentUserService? usuario = null) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, usuario ?? new CurrentUserService());

    private static async Task<Guid> SemearAsync(string nome)
    {
        var db = CriarDb(nome);
        var obra = new Obra { Codigo = "A", Nome = "Obra A" };
        var funcao = new Funcao { Nome = "Pedreiro", Ativo = false }; // função excluída: o nome continua aparecendo
        var ativo = new Ghe { Obra = obra, Numero = 1 };
        ativo.Funcoes.Add(new GheFuncao { Funcao = funcao });
        db.Ghes.AddRange(ativo, new Ghe { Obra = obra, Numero = 1, Ativo = false });
        db.ExamesFuncaoObra.AddRange(
            new ExameFuncaoObra { Obra = obra, Funcao = funcao, Exame = "AUDIOMETRIA", Periodico = true, PeriodicidadeMeses = 12 },
            new ExameFuncaoObra { Obra = obra, Funcao = funcao, Exame = "ANTIGO", Ativo = false });
        await db.SaveChangesAsync();
        return obra.Id;
    }

    [Fact]
    public async Task Ghe_e_exame_desativados_nao_aparecem_e_nome_da_funcao_excluida_sim()
    {
        var nome = nameof(Ghe_e_exame_desativados_nao_aparecem_e_nome_da_funcao_excluida_sim);
        var obraId = await SemearAsync(nome);
        var db = CriarDb(nome);

        var ghe = Assert.Single(await new ListarGhesObraQueryHandler(db).Handle(new ListarGhesObraQuery(obraId), CancellationToken.None));
        Assert.Equal("Pedreiro", Assert.Single(ghe.Funcoes).FuncaoNome);
        var exame = Assert.Single(await new ListarExamesFuncaoObraQueryHandler(db).Handle(new ListarExamesFuncaoObraQuery(obraId, null), CancellationToken.None));
        Assert.Equal("Pedreiro", exame.FuncaoNome);
    }

    [Fact]
    public async Task Usuario_de_outra_obra_nao_ve_a_estrutura_mesmo_informando_o_id()
    {
        var nome = nameof(Usuario_de_outra_obra_nao_ve_a_estrutura_mesmo_informando_o_id);
        var obraId = await SemearAsync(nome);
        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { Guid.NewGuid() });
        var db = CriarDb(nome, restrito);

        Assert.Empty(await new ListarGhesObraQueryHandler(db).Handle(new ListarGhesObraQuery(obraId), CancellationToken.None));
        Assert.Empty(await new ListarExamesFuncaoObraQueryHandler(db).Handle(new ListarExamesFuncaoObraQuery(obraId, null), CancellationToken.None));
    }
}
