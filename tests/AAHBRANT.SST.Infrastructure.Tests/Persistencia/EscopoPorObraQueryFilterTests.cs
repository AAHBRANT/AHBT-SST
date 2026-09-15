using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

public class EscopoPorObraQueryFilterTests
{
    [Fact]
    public async Task Obras_DeveRetornarSomenteObrasPermitidas_QuandoUsuarioNaoTemAcessoGlobal()
    {
        var usuarioAtual = new CurrentUserService();
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new SstDbContext(options, usuarioAtual);

        var obraPermitida = new Obra { Codigo = "OBR-001", Nome = "Obra permitida" };
        var outraObra = new Obra { Codigo = "OBR-002", Nome = "Outra obra" };
        db.Obras.AddRange(obraPermitida, outraObra);
        await db.SaveChangesAsync();

        usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { obraPermitida.Id });

        var obras = await db.Obras.OrderBy(o => o.Nome).ToListAsync();

        var obra = Assert.Single(obras);
        Assert.Equal(obraPermitida.Id, obra.Id);
    }
}
