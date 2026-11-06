using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Seguranca;

// Responsável x Supervisão (06/11/2026): técnico atribuído a UMA obra, com "dds:ver" marcado como
// Global na matriz, vê todas as obras; "dds:criar" marcado como Obra fica só na obra dele.
public class AcessoPorObraServiceTests
{
    private const string Oid = "oid-hilton";

    private static async Task<(AcessoPorObraService Servico, Guid BeiraRio)> PrepararAsync(Guid? obraDoVinculo = null, bool usarVinculoGlobal = false)
    {
        var db = DbContextFactory.Criar();
        var ver = new Permissao { Codigo = "dds:ver", Modulo = "dds", Acao = "ver" };
        var criar = new Permissao { Codigo = "dds:criar", Modulo = "dds", Acao = "criar" };
        var tecnico = new PerfilAcesso { Nome = "Técnico", Tipo = TipoPerfilAcesso.TecnicoSeguranca };
        tecnico.Permissoes.Add(new PerfilAcessoPermissao { Permissao = ver, Escopo = EscopoAcesso.Global });
        tecnico.Permissoes.Add(new PerfilAcessoPermissao { Permissao = criar, Escopo = EscopoAcesso.Obra });
        var usuario = new Usuario { Nome = "Hilton", Email = "h@x.com", AzureAdObjectId = Oid };
        var beiraRio = Guid.NewGuid();
        db.AddRange(ver, criar, tecnico, usuario);
        await db.SaveChangesAsync();
        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra
        {
            UsuarioId = usuario.Id,
            PerfilAcessoId = tecnico.Id,
            ObraId = usarVinculoGlobal ? null : obraDoVinculo ?? beiraRio,
        });
        await db.SaveChangesAsync();
        return (new AcessoPorObraService(db), beiraRio);
    }

    [Fact]
    public async Task ColunaGlobal_VeTodasAsObras()
    {
        var (servico, _) = await PrepararAsync();
        var escopo = await servico.ObterEscopoAsync(Oid, "dds:ver", default);
        Assert.True(escopo.Global);
    }

    [Fact]
    public async Task ColunaObra_EditaSoAObraDoVinculo()
    {
        var (servico, beiraRio) = await PrepararAsync();
        var escopo = await servico.ObterEscopoAsync(Oid, "dds:criar", default);
        Assert.False(escopo.Global);
        Assert.True(escopo.Permite(beiraRio));
        Assert.False(escopo.Permite(Guid.NewGuid()));
    }

    [Fact]
    public async Task VinculoSemObra_ContinuaGlobal()
    {
        var (servico, _) = await PrepararAsync(usarVinculoGlobal: true);
        var escopo = await servico.ObterEscopoAsync(Oid, "dds:criar", default);
        Assert.True(escopo.Global);
    }

    [Fact]
    public async Task SemPermissao_NaoTemEscopo()
    {
        var (servico, _) = await PrepararAsync();
        var escopo = await servico.ObterEscopoAsync(Oid, "dds:encerrar", default);
        Assert.False(escopo.TemPermissao);
    }
}
