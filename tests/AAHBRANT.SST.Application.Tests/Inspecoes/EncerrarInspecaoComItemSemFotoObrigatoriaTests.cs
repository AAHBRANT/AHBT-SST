using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Task 6 (Alojamento em Inspeções, 2026-09-15): item de checklist com ExigeFotografia = true
// bloqueia o encerramento da inspeção enquanto não tiver foto registrada.
public class EncerrarInspecaoComItemSemFotoObrigatoriaTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ItemExigeFotoSemFoto_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_ItemExigeFotoSemFoto_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "responsavel@aahbrant.com", Nome = "Responsável Teste" };
        var checklist = new ChecklistModelo { Nome = "Checklist Alojamento", TipoInspecao = TipoInspecao.Alojamento };
        var item = new ChecklistModeloItem
        {
            Ordem = 1,
            Descricao = "Piso resistente, lavável e impermeável",
            ExigeFotografia = true,
        };
        checklist.Itens.Add(item);
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta
        {
            ChecklistModeloItemId = item.Id,
            StatusItem = StatusItemChecklist.Conforme,
            // FotoConteudo não preenchido: fica no default Array.Empty<byte>() do modelo.
        });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        var handler = new EncerrarInspecaoCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new EncerrarInspecaoCommand(inspecao.Id), default));

        Assert.Contains("exigem foto", ex.Message);
        Assert.Contains(item.Descricao, ex.Message);
    }

    [Fact]
    public async Task Handle_AlojamentoItemExigeFotoSemFotoPosterior_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_AlojamentoItemExigeFotoSemFotoPosterior_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB2", Nome = "Obra Teste 2" };
        var usuario = new Usuario { Email = "responsavel2@aahbrant.com", Nome = "Responsável Teste 2" };
        var checklist = new ChecklistModelo { Nome = "Checklist Alojamento", TipoInspecao = TipoInspecao.Alojamento };
        var item = new ChecklistModeloItem
        {
            Ordem = 1,
            Descricao = "Piso resistente, lavável e impermeável",
            ExigeFotografia = true,
        };
        checklist.Itens.Add(item);
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta
        {
            ChecklistModeloItemId = item.Id,
            StatusItem = StatusItemChecklist.Conforme,
            FotoConteudo = new byte[] { 1, 2, 3 },
            FotoContentType = "image/jpeg",
        });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        var handler = new EncerrarInspecaoCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new EncerrarInspecaoCommand(inspecao.Id), default));

        Assert.Contains("evidência posterior", ex.Message);
        Assert.Contains(item.Descricao, ex.Message);
    }

    [Fact]
    public async Task Handle_AlojamentoItemExigeFotoComDuasFotos_EncerraSemErro()
    {
        var db = CriarDb(nameof(Handle_AlojamentoItemExigeFotoComDuasFotos_EncerraSemErro));
        var obra = new Obra { Codigo = "OB3", Nome = "Obra Teste 3" };
        var usuario = new Usuario { Email = "responsavel3@aahbrant.com", Nome = "Responsável Teste 3" };
        var checklist = new ChecklistModelo { Nome = "Checklist Alojamento", TipoInspecao = TipoInspecao.Alojamento };
        var item = new ChecklistModeloItem
        {
            Ordem = 1,
            Descricao = "Piso resistente, lavável e impermeável",
            ExigeFotografia = true,
        };
        checklist.Itens.Add(item);
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta
        {
            ChecklistModeloItemId = item.Id,
            StatusItem = StatusItemChecklist.Conforme,
            FotoConteudo = new byte[] { 1, 2, 3 },
            FotoContentType = "image/jpeg",
            FotoDepoisConteudo = new byte[] { 4, 5, 6 },
            FotoDepoisContentType = "image/jpeg",
        });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();

        var handler = new EncerrarInspecaoCommandHandler(db);
        await handler.Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        var atualizada = await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id);
        Assert.Equal(StatusInspecao.Concluida, atualizada.Status);
    }
}
