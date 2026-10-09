using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Os testes de escopo rodam em InMemory, que executa qualquer LINQ — o SQL Server não. Já perdemos
// produção por consulta que só falhava na tradução (ver ResolverDocumentoPublicoQuery). Aqui só se
// gera o SQL (ToQueryString, sem conexão) das consultas de escopo, com usuário RESTRITO — o caminho
// global não monta subconsulta nenhuma.
public class EscopoObraTraducaoSqlTests
{
    private static SstDbContext CriarContextoSqlServerRestrito()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>()
            .UseSqlServer("Server=nao-conecta;Database=traducao;Trusted_Connection=True;")
            .Options;
        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(false, new[] { Guid.NewGuid() });
        return new SstDbContext(opcoes, usuario);
    }

    [Fact]
    public void NoEscopoDoTrabalhador_TraduzParaSqlServer()
    {
        using var db = CriarContextoSqlServerRestrito();

        var sqlAso = db.Asos.NoEscopoDoTrabalhador(db, a => a.TrabalhadorId).Where(a => a.Id == Guid.Empty).ToQueryString();
        var sqlFoto = db.DocumentoSignatarios.NoEscopoDoTrabalhador(db, s => s.TrabalhadorId).ToQueryString();

        Assert.Contains("[Trabalhadores]", sqlAso);
        Assert.Contains("[Trabalhadores]", sqlFoto);
    }

    [Theory]
    [InlineData("Dds")]
    [InlineData("DdsSemanal")]
    [InlineData("Inspecao")]
    [InlineData("SessaoTreinamento")]
    [InlineData("ProcessoEleitoralCipa")]
    [InlineData("ReuniaoCipa")]
    [InlineData("Apr")]
    [InlineData("PermissaoTrabalho")]
    [InlineData("NaoConformidade")]
    [InlineData("Treinamento")]
    [InlineData("EntregaEpi")]
    [InlineData("DevolucaoEpi")]
    [InlineData("EntregaUniforme")]
    [InlineData("FichaEpiTrabalhador")]
    [InlineData("TermoCompromissoEpi")]
    public void DocumentoAssinatura_TodoTipoConhecido_TraduzParaSqlServer(string entidadeTipo)
    {
        using var db = CriarContextoSqlServerRestrito();

        var consulta = EscopoDocumentoAssinatura.ConsultaEntidadesNoEscopo(db, entidadeTipo, new[] { Guid.NewGuid() });

        Assert.NotNull(consulta);
        Assert.False(string.IsNullOrWhiteSpace(consulta!.ToQueryString()));
    }

    // Todo tipo com rótulo no Motor de Assinatura precisa de regra de escopo — senão o documento
    // some do painel para usuário restrito (falha fechada) sem ninguém perceber.
    [Fact]
    public void DocumentoAssinatura_TipoDesconhecido_FicaInvisivel()
    {
        using var db = CriarContextoSqlServerRestrito();

        Assert.Null(EscopoDocumentoAssinatura.ConsultaEntidadesNoEscopo(db, "TipoAindaNaoMapeado", new[] { Guid.NewGuid() }));
    }
}
