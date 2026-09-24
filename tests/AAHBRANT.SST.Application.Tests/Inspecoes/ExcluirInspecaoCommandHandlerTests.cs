using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Inspeção não é registro isolado: um item reprovado vira Não Conformidade, que tem prazo,
// responsável e plano de ação próprios. Estes testes travam a decisão de 24/09 — a exclusão é
// recusada enquanto existir NC vinculada, em vez de levar esse trabalho junto em cascata.
public class ExcluirInspecaoCommandHandlerTests
{
    private static async Task<(Inspecao inspecao, InspecaoItemResposta resposta)> SemearAsync(SstDbContext db)
    {
        var inspecao = new Inspecao
        {
            ObraId = Guid.NewGuid(),
            ChecklistModeloId = Guid.NewGuid(),
            ResponsavelUsuarioId = Guid.NewGuid(),
        };
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        var resposta = new InspecaoItemResposta
        {
            InspecaoId = inspecao.Id,
            ChecklistModeloItemId = Guid.NewGuid(),
        };
        db.InspecaoItemRespostas.Add(resposta);
        await db.SaveChangesAsync();
        return (inspecao, resposta);
    }

    [Fact]
    public async Task Handle_InspecaoSemNaoConformidade_ExcluiInspecaoERespostas()
    {
        var db = DbContextFactory.Criar();
        var (inspecao, _) = await SemearAsync(db);
        var handler = new ExcluirInspecaoCommandHandler(db);

        await handler.Handle(new ExcluirInspecaoCommand(inspecao.Id), default);

        Assert.Empty(db.Inspecoes);
        // As respostas saem junto: ficariam ativas apontando para uma inspeção que não existe mais.
        Assert.Empty(db.InspecaoItemRespostas);
    }

    [Fact]
    public async Task Handle_InspecaoComNaoConformidadeGerada_RecusaEPreservaTudo()
    {
        var db = DbContextFactory.Criar();
        var (inspecao, resposta) = await SemearAsync(db);
        db.NaoConformidades.Add(new NaoConformidade
        {
            Descricao = "Guarda-corpo ausente no pavimento 3",
            InspecaoItemRespostaId = resposta.Id,
        });
        await db.SaveChangesAsync();
        var handler = new ExcluirInspecaoCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExcluirInspecaoCommand(inspecao.Id), default));

        Assert.Contains("1 não conformidade", ex.Message);
        Assert.Single(db.Inspecoes);
        Assert.Single(db.NaoConformidades);
    }

    // Não conformidade avulsa (registrada direto, sem vir de inspeção) não segura exclusão nenhuma.
    [Fact]
    public async Task Handle_NaoConformidadeAvulsa_NaoImpedeExclusao()
    {
        var db = DbContextFactory.Criar();
        var (inspecao, _) = await SemearAsync(db);
        db.NaoConformidades.Add(new NaoConformidade
        {
            Descricao = "Registrada direto pela tela, sem inspeção",
            InspecaoItemRespostaId = null,
        });
        await db.SaveChangesAsync();
        var handler = new ExcluirInspecaoCommandHandler(db);

        await handler.Handle(new ExcluirInspecaoCommand(inspecao.Id), default);

        Assert.Empty(db.Inspecoes);
        Assert.Single(db.NaoConformidades);
    }

    [Fact]
    public async Task Handle_InspecaoInexistente_LancaKeyNotFound()
    {
        var db = DbContextFactory.Criar();
        var handler = new ExcluirInspecaoCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ExcluirInspecaoCommand(Guid.NewGuid()), default));
    }
}
