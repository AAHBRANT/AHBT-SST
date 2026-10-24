using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Empresas;

public class CriarEmpresaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_DadosValidos_CriaEmpresa()
    {
        var db = CriarDb(nameof(Handle_DadosValidos_CriaEmpresa));
        var handler = new CriarEmpresaCommandHandler(db);

        var id = await handler.Handle(
            new CriarEmpresaCommand("Construtora XPTO Ltda", "XPTO", "12345678000199", "Elétrica", "João", "11999990000", "joao@xpto.com"),
            default);

        var empresa = await db.Empresas.FirstAsync(e => e.Id == id);
        Assert.Equal("Construtora XPTO Ltda", empresa.RazaoSocial);
        Assert.Equal("12345678000199", empresa.Cnpj);
    }

    [Fact]
    public async Task Handle_CnpjJaCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_CnpjJaCadastrado_LancaInvalidOperationException));
        db.Empresas.Add(new Empresa { RazaoSocial = "Já existe", Cnpj = "12345678000199" });
        await db.SaveChangesAsync();
        var handler = new CriarEmpresaCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CriarEmpresaCommand("Outra", null, "12345678000199", null, null, null, null), default));
    }
}
