using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// A finalização falhava sempre no SQL Server (InMemory aceitava): OrderBy sobre o construtor do DTO não
// traduz. Em hml nenhum documento tinha sido finalizado até 30/10. Só gera o SQL, sem conexão.
public class FinalizarDocumentoTraducaoSqlTests
{
    private static SstDbContext CriarContextoSqlServer()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>()
            .UseSqlServer("Server=nao-conecta;Database=traducao;Trusted_Connection=True;")
            .Options;
        return new SstDbContext(opcoes, new CurrentUserService());
    }

    [Fact]
    public void ConsultaDosSignatarios_TraduzParaSqlServer()
    {
        using var db = CriarContextoSqlServer();
        var documentoId = Guid.NewGuid();

        var sql = db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documentoId)
            .OrderBy(s => s.AssinadoEm)
            .Join(db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new { t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress })
            .ToQueryString();

        Assert.Contains("[DocumentoSignatarios]", sql);
    }

    [Fact]
    public void FormaAntiga_NaoTraduz()
    {
        using var db = CriarContextoSqlServer();
        var documentoId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documentoId)
            .Join(db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoSignatarioDto(t.Id, t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress))
            .OrderBy(s => s.AssinadoEm)
            .ToQueryString());
    }
}
