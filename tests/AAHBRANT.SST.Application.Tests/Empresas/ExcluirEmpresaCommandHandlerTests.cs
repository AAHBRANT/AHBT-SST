using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Empresas.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Empresas;

public class ExcluirEmpresaCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_EmpresaExistente_MarcaAtivoFalse()
    {
        var db = CriarDb(nameof(Handle_EmpresaExistente_MarcaAtivoFalse));
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        var handler = new ExcluirEmpresaCommandHandler(db);

        await handler.Handle(new ExcluirEmpresaCommand(empresa.Id), default);

        var atualizada = await db.Empresas.IgnoreQueryFilters().FirstAsync(e => e.Id == empresa.Id);
        Assert.False(atualizada.Ativo);
    }
}
