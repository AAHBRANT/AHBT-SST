using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class TemplateBiometricoFutronicConfiguracaoTests
{
    [Fact]
    public async Task DevePersistirTemplateComTextoOpaco()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(DevePersistirTemplateComTextoOpaco));

        var obra = new Obra { Codigo = "OBR-002", Nome = "Obra Teste 2" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Matricula = "M-001", Cpf = "12345678901" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        var template = new TemplateBiometricoFutronic
        {
            TrabalhadorId = trabalhador.Id,
            TemplateCriptografado = "base64-fake-cifrado",
            CapturadoEm = DateTime.UtcNow,
        };
        db.TemplatesBiometricoFutronic.Add(template);
        await db.SaveChangesAsync();

        var recuperado = await db.TemplatesBiometricoFutronic.FirstOrDefaultAsync(t => t.Id == template.Id);

        Assert.NotNull(recuperado);
        Assert.Equal("base64-fake-cifrado", recuperado!.TemplateCriptografado);
    }
}
