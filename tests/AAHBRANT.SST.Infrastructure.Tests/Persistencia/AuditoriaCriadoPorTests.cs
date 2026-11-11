using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

public class AuditoriaCriadoPorTests
{
    private static (SstDbContext Db, CurrentUserService Usuario, DbContextOptions<SstDbContext> Options) Criar()
    {
        var usuario = new CurrentUserService();
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return (new SstDbContext(options, usuario), usuario, options);
    }

    [Fact]
    public async Task Criar_ComUsuario_PreencheCreatedBy()
    {
        var (db, usuario, _) = Criar();
        await using var _ = db;
        var id = Guid.NewGuid();
        usuario.DefinirUsuarioId(id);
        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        Assert.Equal(id, obra.CreatedBy);
        Assert.Null(obra.UpdatedBy);
    }

    [Fact]
    public async Task Atualizar_ComUsuario_PreencheUpdatedBy()
    {
        var (db, usuario, _) = Criar();
        await using var _ = db;
        var criador = Guid.NewGuid();
        var editor = Guid.NewGuid();
        usuario.DefinirUsuarioId(criador);
        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();

        usuario.DefinirUsuarioId(editor);
        obra.Nome = "Obra renomeada";
        await db.SaveChangesAsync();

        Assert.Equal(criador, obra.CreatedBy);
        Assert.Equal(editor, obra.UpdatedBy);
    }

    [Fact]
    public async Task SemUsuario_FicaNulo()
    {
        var (db, _, _) = Criar();
        await using var _ = db;
        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        obra.Nome = "Outro";
        await db.SaveChangesAsync();
        Assert.Null(obra.CreatedBy);
        Assert.Null(obra.UpdatedBy);
    }

    [Fact]
    public async Task ValorJaDefinidoPeloCodigo_NaoESobrescrito()
    {
        var (db, usuario, _) = Criar();
        await using var _ = db;
        usuario.DefinirUsuarioId(Guid.NewGuid());
        var definido = Guid.NewGuid();
        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra", CreatedBy = definido };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        Assert.Equal(definido, obra.CreatedBy);

        var outro = Guid.NewGuid();
        obra.Nome = "Alterada";
        obra.UpdatedBy = outro;
        await db.SaveChangesAsync();
        Assert.Equal(outro, obra.UpdatedBy);
    }

    [Fact]
    public async Task ExclusaoLogica_PreencheUpdatedBy()
    {
        var (db, usuario, _) = Criar();
        await using var _ = db;
        var id = Guid.NewGuid();
        usuario.DefinirUsuarioId(id);
        var obra = new Obra { Codigo = "OBR-001", Nome = "Obra" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        db.Obras.Remove(obra);
        await db.SaveChangesAsync();
        Assert.False(obra.Ativo);
        Assert.Equal(id, obra.UpdatedBy);
    }
}
