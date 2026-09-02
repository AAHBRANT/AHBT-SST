using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Riscos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Riscos;

public class ImportarRiscosLoteCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, MatrizRiscoConfig Matriz)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "OBRA-1", Nome = "Obra Teste" };
        db.Obras.Add(obra);

        var matriz = new MatrizRiscoConfig { Nome = "Matriz Teste", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = 1, Severidade = 3, NivelRisco = NivelRisco.Baixo });
        matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = 3, Severidade = 2, NivelRisco = NivelRisco.Moderado });
        db.MatrizRiscoConfigs.Add(matriz);

        await db.SaveChangesAsync();
        return (obra, matriz);
    }

    [Fact]
    public async Task Cria_atividade_e_perigo_novos_quando_nao_existem()
    {
        var db = CriarDb(nameof(Cria_atividade_e_perigo_novos_quando_nao_existem));
        var (obra, _) = await SemearAsync(db);

        var handler = new ImportarRiscosLoteCommandHandler(db);
        var resultado = await handler.Handle(new ImportarRiscosLoteCommand(obra.Id, new List<ImportarRiscoLoteItem>
        {
            new("Alvenaria", "Descrição da atividade", "Ruído do ambiente", "Físico", "Céu aberto", "Qualitativa",
                "Redução da audição", 1, 3, "EPC/MA: sinalização | EPI: protetor auricular", "Monitoramento"),
        }), CancellationToken.None);

        Assert.Equal(1, resultado.AtividadesCriadas);
        Assert.Equal(1, resultado.PerigosCriados);
        Assert.Equal(1, resultado.RiscosCriados);

        var atividade = Assert.Single(await db.Atividades.Where(a => a.ObraId == obra.Id).ToListAsync());
        Assert.Equal("Alvenaria", atividade.Nome);

        var risco = Assert.Single(await db.Riscos.ToListAsync());
        Assert.Equal(NivelRisco.Baixo, risco.NivelRisco);
    }

    [Fact]
    public async Task Reaproveita_atividade_e_perigo_ja_existentes_em_vez_de_duplicar()
    {
        var db = CriarDb(nameof(Reaproveita_atividade_e_perigo_ja_existentes_em_vez_de_duplicar));
        var (obra, _) = await SemearAsync(db);

        var atividadeExistente = new Atividade { ObraId = obra.Id, Nome = "Alvenaria" };
        var perigoExistente = new Perigo { Nome = "Ruído do ambiente", Agente = "Físico" };
        db.Atividades.Add(atividadeExistente);
        db.Perigos.Add(perigoExistente);
        await db.SaveChangesAsync();

        var handler = new ImportarRiscosLoteCommandHandler(db);
        var resultado = await handler.Handle(new ImportarRiscosLoteCommand(obra.Id, new List<ImportarRiscoLoteItem>
        {
            new("Alvenaria", null, "Ruído do ambiente", null, null, null, null, 3, 2, null, null),
        }), CancellationToken.None);

        Assert.Equal(0, resultado.AtividadesCriadas);
        Assert.Equal(0, resultado.PerigosCriados);
        Assert.Equal(1, resultado.RiscosCriados);
        Assert.Single(await db.Atividades.ToListAsync());
        Assert.Single(await db.Perigos.ToListAsync());

        var risco = Assert.Single(await db.Riscos.ToListAsync());
        Assert.Equal(atividadeExistente.Id, risco.AtividadeId);
        Assert.Equal(perigoExistente.Id, risco.PerigoId);
        Assert.Equal(NivelRisco.Moderado, risco.NivelRisco);
    }

    [Fact]
    public async Task Lanca_erro_quando_matriz_nao_tem_celula_para_combinacao()
    {
        var db = CriarDb(nameof(Lanca_erro_quando_matriz_nao_tem_celula_para_combinacao));
        var (obra, _) = await SemearAsync(db);

        var handler = new ImportarRiscosLoteCommandHandler(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ImportarRiscosLoteCommand(obra.Id, new List<ImportarRiscoLoteItem>
        {
            new("Alvenaria", null, "Ruído do ambiente", null, null, null, null, 5, 5, null, null),
        }), CancellationToken.None));
    }
}
