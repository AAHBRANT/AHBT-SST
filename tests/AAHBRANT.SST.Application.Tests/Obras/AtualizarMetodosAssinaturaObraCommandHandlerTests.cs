using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Obras.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Obras;

public class AtualizarMetodosAssinaturaObraCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<Obra> SemearObraAsync(IAppDbContext db, MetodoAutenticacaoObra inicial)
    {
        var obra = new Obra { Codigo = "OB-1", Nome = "Obra Teste", MetodosAutenticacaoHabilitados = inicial };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        return obra;
    }

    [Theory]
    [InlineData(true, false, MetodoAutenticacaoObra.Biometria)]
    [InlineData(false, true, MetodoAutenticacaoObra.ReconhecimentoFacial)]
    [InlineData(true, true, MetodoAutenticacaoObra.Biometria | MetodoAutenticacaoObra.ReconhecimentoFacial)]
    [InlineData(false, false, MetodoAutenticacaoObra.Nenhum)]
    public async Task Handle_DefineExatamenteOsMetodosInformados(bool biometria, bool facial, MetodoAutenticacaoObra esperado)
    {
        var db = CriarDb($"{nameof(Handle_DefineExatamenteOsMetodosInformados)}-{biometria}-{facial}");
        var obra = await SemearObraAsync(db, MetodoAutenticacaoObra.ReconhecimentoFacial);

        await new AtualizarMetodosAssinaturaObraCommandHandler(db)
            .Handle(new AtualizarMetodosAssinaturaObraCommand(obra.Id, biometria, facial), CancellationToken.None);

        var salva = await db.Obras.SingleAsync(o => o.Id == obra.Id);
        Assert.Equal(esperado, salva.MetodosAutenticacaoHabilitados);
    }

    [Fact]
    public async Task Handle_ObraInexistente_LancaKeyNotFound()
    {
        var db = CriarDb(nameof(Handle_ObraInexistente_LancaKeyNotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new AtualizarMetodosAssinaturaObraCommandHandler(db)
                .Handle(new AtualizarMetodosAssinaturaObraCommand(Guid.NewGuid(), true, false), CancellationToken.None));
    }
}
