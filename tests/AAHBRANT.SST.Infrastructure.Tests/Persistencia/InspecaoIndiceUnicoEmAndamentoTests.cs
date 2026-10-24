using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests;

// Regressão (05/10/2026): excluir uma inspeção é soft delete (Ativo = false) e o Status continua
// "Em andamento". O índice único filtrado só olhava Status, então a linha excluída seguia ocupando a
// vaga e "Nova inspeção" do mesmo veículo/alojamento falhava com 500 (duplicate key). O query filter
// global esconde a linha excluída do handler, que não conseguia nem reaproveitá-la. O índice precisa
// ignorar linhas inativas.
public class InspecaoIndiceUnicoEmAndamentoTests
{
    private static string? FiltroDoIndice(string propriedade)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nameof(InspecaoIndiceUnicoEmAndamentoTests) + propriedade).Options;
        using var db = new SstDbContext(options, new CurrentUserService());
        var entidade = db.Model.FindEntityType(typeof(Inspecao))!;
        var indice = entidade.GetIndexes().Single(i => i.IsUnique && i.Properties.Single().Name == propriedade);
        return indice.GetFilter();
    }

    [Theory]
    [InlineData(nameof(Inspecao.VeiculoId))]
    [InlineData(nameof(Inspecao.AlojamentoId))]
    public void IndiceUnicoDeInspecaoEmAndamento_IgnoraRegistrosExcluidos(string propriedade)
    {
        var filtro = FiltroDoIndice(propriedade);

        Assert.NotNull(filtro);
        Assert.Contains("[Ativo] = 1", filtro);
        Assert.Contains("[Status] = 1", filtro);
    }
}
