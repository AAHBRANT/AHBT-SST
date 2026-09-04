using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Riscos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Riscos;

public class LimparRiscosObraCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Remove_apenas_os_riscos_da_obra_informada_preservando_atividade_e_perigo()
    {
        var db = CriarDb(nameof(Remove_apenas_os_riscos_da_obra_informada_preservando_atividade_e_perigo));

        var obraAlvo = new Obra { Codigo = "OBRA-1", Nome = "Obra Alvo" };
        var obraOutra = new Obra { Codigo = "OBRA-2", Nome = "Outra Obra" };
        db.Obras.Add(obraAlvo);
        db.Obras.Add(obraOutra);

        var atividadeAlvo = new Atividade { ObraId = obraAlvo.Id, Nome = "Alvenaria" };
        var atividadeOutra = new Atividade { ObraId = obraOutra.Id, Nome = "Alvenaria" };
        db.Atividades.Add(atividadeAlvo);
        db.Atividades.Add(atividadeOutra);

        var perigo = new Perigo { Nome = "Ruído do ambiente", Agente = "Físico" };
        db.Perigos.Add(perigo);

        db.Riscos.Add(new Risco { Atividade = atividadeAlvo, Perigo = perigo, Probabilidade = 1, Severidade = 3 });
        db.Riscos.Add(new Risco { Atividade = atividadeAlvo, Perigo = perigo, Probabilidade = 1, Severidade = 3 });
        db.Riscos.Add(new Risco { Atividade = atividadeOutra, Perigo = perigo, Probabilidade = 1, Severidade = 3 });
        await db.SaveChangesAsync();

        var handler = new LimparRiscosObraCommandHandler(db);
        var quantidadeRemovida = await handler.Handle(new LimparRiscosObraCommand(obraAlvo.Id), CancellationToken.None);

        Assert.Equal(2, quantidadeRemovida);
        Assert.Empty(await db.Riscos.Where(r => r.AtividadeId == atividadeAlvo.Id).ToListAsync());
        Assert.Single(await db.Riscos.Where(r => r.AtividadeId == atividadeOutra.Id).ToListAsync());
        Assert.Single(await db.Atividades.Where(a => a.ObraId == obraAlvo.Id).ToListAsync());
        Assert.Single(await db.Perigos.ToListAsync());
    }

    [Fact]
    public async Task Lanca_erro_quando_obra_nao_existe()
    {
        var db = CriarDb(nameof(Lanca_erro_quando_obra_nao_existe));
        var handler = new LimparRiscosObraCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new LimparRiscosObraCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
