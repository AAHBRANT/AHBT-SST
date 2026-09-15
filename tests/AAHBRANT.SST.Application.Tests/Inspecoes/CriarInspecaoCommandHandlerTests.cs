using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

public class CriarInspecaoCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_CriaInspecao_GeraNumeroDocumentoComPrefixoINSP()
    {
        var db = CriarDb(nameof(Handle_CriaInspecao_GeraNumeroDocumentoComPrefixoINSP));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "responsavel@aahbrant.com", Nome = "Responsável Teste" };
        var checklist = new ChecklistModelo { Nome = "Checklist Andaimes", TipoInspecao = Domain.Enums.TipoInspecao.Andaimes };
        db.Obras.Add(obra);
        db.Usuarios.Add(usuario);
        db.ChecklistModelos.Add(checklist);
        await db.SaveChangesAsync();

        var handler = new CriarInspecaoCommandHandler(db, new GeradorNumeroDocumentoService(db));
        var id = await handler.Handle(new CriarInspecaoCommand(checklist.Id, obra.Id, null, DateTime.UtcNow, usuario.Id), default);

        var inspecao = await db.Inspecoes.FirstAsync(i => i.Id == id);
        Assert.StartsWith("INSP-", inspecao.NumeroDocumento);
    }
}
