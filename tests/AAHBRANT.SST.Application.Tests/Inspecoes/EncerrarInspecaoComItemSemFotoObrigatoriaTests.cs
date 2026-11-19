using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

using AAHBRANT.SST.Application.Tests.TestSupport;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// 18/11/2026: todo item do checklist exige 2 fotos (foto principal + 1 foto adicional) para encerrar,
// independente de ExigeFotografia. A "foto posterior" (FotoDepois) não conta como 2ª foto.
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

        Assert.Contains("exige 2 fotos", ex.Message);
        Assert.Contains(item.Descricao, ex.Message);
    }

    [Fact]
    public async Task Handle_ItemComUmaFotoSo_Recusa()
    {
        var db = CriarDb(nameof(Handle_ItemComUmaFotoSo_Recusa));
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

        // Só a foto principal: falta a 2ª.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new EncerrarInspecaoCommand(inspecao.Id), default));
        Assert.Contains("exige 2 fotos", ex.Message);
        Assert.Contains("Piso resistente", ex.Message);
    }

    [Fact]
    public async Task Handle_ItemComDuasFotos_Encerra()
    {
        var db = CriarDb(nameof(Handle_ItemComDuasFotos_Encerra));
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
        await FotosInspecaoDeTeste.CompletarAsync(db, inspecao.Id);

        var handler = new EncerrarInspecaoCommandHandler(db);
        await handler.Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        var atualizada = await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id);
        Assert.Equal(StatusInspecao.AguardandoAssinatura, atualizada.Status);
    }
}
