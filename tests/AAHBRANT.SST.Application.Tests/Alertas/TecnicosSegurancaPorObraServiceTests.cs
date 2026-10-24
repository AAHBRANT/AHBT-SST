using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alertas;

public class TecnicosSegurancaPorObraServiceTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task ObterUsuarioIdsAsync_TecnicoDaObraETecnicoGlobal_RetornaAmbos_IgnoraTecnicoDeOutraObra()
    {
        var db = CriarDb(nameof(ObterUsuarioIdsAsync_TecnicoDaObraETecnicoGlobal_RetornaAmbos_IgnoraTecnicoDeOutraObra));
        var obraA = new Obra { Codigo = "A", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "B", Nome = "Obra B" };
        var perfilTecnico = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico de Segurança" };
        var perfilEncarregado = new PerfilAcesso { Tipo = TipoPerfilAcesso.Encarregado, Nome = "Encarregado" };
        var tecnicoDaObraA = new Usuario { Nome = "Téc A", Email = "a@x.com" };
        var tecnicoGlobal = new Usuario { Nome = "Téc Global", Email = "g@x.com" };
        var tecnicoDaObraB = new Usuario { Nome = "Téc B", Email = "b@x.com" };
        var encarregadoDaObraA = new Usuario { Nome = "Encarregado A", Email = "e@x.com" };
        db.Obras.AddRange(obraA, obraB);
        db.PerfisAcesso.AddRange(perfilTecnico, perfilEncarregado);
        db.Usuarios.AddRange(tecnicoDaObraA, tecnicoGlobal, tecnicoDaObraB, encarregadoDaObraA);
        await db.SaveChangesAsync();

        db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = tecnicoDaObraA.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = obraA.Id },
            new UsuarioPerfilObra { UsuarioId = tecnicoGlobal.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = null },
            new UsuarioPerfilObra { UsuarioId = tecnicoDaObraB.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = obraB.Id },
            new UsuarioPerfilObra { UsuarioId = encarregadoDaObraA.Id, PerfilAcessoId = perfilEncarregado.Id, ObraId = obraA.Id });
        await db.SaveChangesAsync();

        var servico = new TecnicosSegurancaPorObraService(db);
        var resultado = await servico.ObterUsuarioIdsAsync(obraA.Id);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(tecnicoDaObraA.Id, resultado);
        Assert.Contains(tecnicoGlobal.Id, resultado);
        Assert.DoesNotContain(tecnicoDaObraB.Id, resultado);
        Assert.DoesNotContain(encarregadoDaObraA.Id, resultado);
    }
}
