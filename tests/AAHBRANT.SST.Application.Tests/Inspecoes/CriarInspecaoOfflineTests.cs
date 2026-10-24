using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

public class CriarInspecaoOfflineTests
{
    private static async Task<(SstDbContext Db, CriarInspecaoCommand Command)> Preparar()
    {
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new CurrentUserService());
        var obra = new Obra { Nome = "Obra offline", Codigo = "OFF" };
        var usuario = new Usuario { Nome = "Técnico", Email = "tecnico@teste.com" };
        var modelo = new ChecklistModelo { Nome = "Modelo offline", Versao = 1, TipoInspecao = TipoInspecao.Obra };
        modelo.Itens.Add(new ChecklistModeloItem { Ordem = 1, Descricao = "Proteção coletiva" });
        modelo.Itens.Add(new ChecklistModeloItem { Ordem = 2, Descricao = "Sinalização" });
        db.Obras.Add(obra); db.Usuarios.Add(usuario); db.ChecklistModelos.Add(modelo);
        await db.SaveChangesAsync();
        return (db, new(modelo.Id, obra.Id, null, DateTime.UtcNow, usuario.Id, Guid.NewGuid(),
            modelo.Itens.Select(i => new RespostaInspecaoCliente(i.Id, Guid.NewGuid())).ToList(), 1));
    }

    [Fact]
    public async Task Criacao_PreservaIdsDoTabletEGeraProtocoloNoServidor()
    {
        var (db, command) = await Preparar();
        using (db)
        {
            var criada = await new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db)).Handle(command, default);
            Assert.Equal(command.IdCliente, criada.Id);
            Assert.StartsWith("INSP-", criada.NumeroDocumento);
            var ids = await db.InspecaoItemRespostas.Where(r => r.InspecaoId == criada.Id).Select(r => r.Id).ToListAsync();
            Assert.Equal(command.RespostasCliente!.Select(r => r.Id).Order(), ids.Order());
            Assert.Equal(ids.Order(), criada.VersoesRespostas.Keys.Order());
        }
    }

    [Fact]
    public async Task IdentificadorExistente_NaoCriaDuplicataNemVinculaOutraInspecao()
    {
        var (db, command) = await Preparar();
        using (db)
        {
            var handler = new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db));
            await handler.Handle(command, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, default));
            Assert.Equal(1, await db.Inspecoes.CountAsync());
            Assert.Equal(2, await db.InspecaoItemRespostas.CountAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapeamentoIncompletoOuRepetido_NaoCriaRegistro(bool repetido)
    {
        var (db, command) = await Preparar();
        using (db)
        {
            var ids = command.RespostasCliente!;
            var invalidos = repetido ? new List<RespostaInspecaoCliente> { ids[0], ids[1] with { Id = ids[0].Id } } : new List<RespostaInspecaoCliente> { ids[0] };
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db))
                .Handle(command with { RespostasCliente = invalidos }, default));
            Assert.Empty(await db.Inspecoes.ToListAsync());
        }
    }

    [Fact]
    public async Task ModeloMudou_ExigeRevisaoAntesDeCriar()
    {
        var (db, command) = await Preparar();
        using (db)
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db))
                .Handle(command with { VersaoChecklistEsperada = 2 }, default));
            Assert.Empty(await db.Inspecoes.ToListAsync());
        }
    }
}
