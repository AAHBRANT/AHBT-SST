using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.ChecklistModelos;

// A Secao vive só em ChecklistModeloItem (template) — este teste garante que ela chega até o
// InspecaoItemRespostaDto (join feito em ObterInspecaoDetalheQueryHandler), que é o que o
// frontend usa pra agrupar os achados por seção na tela de execução.
public class ObterInspecaoDetalheQueryHandlerSecaoTests
{
    [Fact]
    public async Task Handle_ItemComSecaoNoTemplate_ExpoeSecaoNaRespostaDaInspecao()
    {
        var db = DbContextFactory.Criar();

        // Obra e ResponsavelUsuario são navegações obrigatórias (FK Guid não-nulável) em Inspecao,
        // e ambas as entidades têm filtro global HasQueryFilter(Ativo) — sem uma linha real
        // casando o Id, o Include vira um inner join que zera a Inspecao inteira, não só a
        // navegação. Precisam existir de verdade, não só o Guid solto.
        var obra = new Obra { Codigo = "OBRA-001", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "responsavel@teste.com", Nome = "Responsável Teste" };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);

        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        var item = new ChecklistModeloItem { ChecklistModelo = checklist, Ordem = 1, Descricao = "Cama individual", Secao = "Dormitórios" };
        checklist.Itens.Add(item);
        db.ChecklistModelos.Add(checklist);

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            ChecklistModeloId = checklist.Id,
            ChecklistModelo = checklist,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta { Inspecao = inspecao, ChecklistModeloItemId = item.Id, ChecklistModeloItem = item });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        var handler = new ObterInspecaoDetalheQueryHandler(db);
        var detalhe = await handler.Handle(new ObterInspecaoDetalheQuery(inspecao.Id), default);

        Assert.NotNull(detalhe);
        var resposta = Assert.Single(detalhe!.Respostas);
        Assert.Equal("Dormitórios", resposta.Secao);
    }

    // Bug 24/09/2026: criar nova versão do checklist no Catálogo (CriarNovaVersaoChecklistModelo)
    // marca a versão anterior como Ativo = false. Inspeções em andamento que apontavam pra ela
    // davam "Not Found" ao clicar "Continuar inspeção" — o Include(ChecklistModelo) virava inner
    // join filtrado e sumia com a inspeção inteira. O mesmo vale para responsável desativado.
    [Fact]
    public async Task Handle_ChecklistVersaoAnteriorEResponsavelInativos_AindaDevolveInspecao()
    {
        var db = DbContextFactory.Criar();

        var obra = new Obra { Codigo = "OBRA-001", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "responsavel@teste.com", Nome = "Responsável Teste" };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);

        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        var item = new ChecklistModeloItem { ChecklistModelo = checklist, Ordem = 1, Descricao = "Cama individual" };
        checklist.Itens.Add(item);
        db.ChecklistModelos.Add(checklist);

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta { Inspecao = inspecao, ChecklistModeloItemId = item.Id });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        checklist.Ativo = false;
        usuario.Ativo = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = new ObterInspecaoDetalheQueryHandler(db);
        var detalhe = await handler.Handle(new ObterInspecaoDetalheQuery(inspecao.Id), default);

        Assert.NotNull(detalhe);
        Assert.Equal("Checklist de Alojamento", detalhe!.Inspecao.ChecklistModeloNome);
        Assert.Equal("Responsável Teste", detalhe.Inspecao.ResponsavelUsuarioNome);
        var resposta = Assert.Single(detalhe.Respostas);
        Assert.Equal("Cama individual", resposta.Descricao);
    }
}
