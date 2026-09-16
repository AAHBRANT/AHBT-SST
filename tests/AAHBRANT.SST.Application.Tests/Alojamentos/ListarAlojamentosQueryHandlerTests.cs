using AAHBRANT.SST.Application.Alojamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class ListarAlojamentosQueryHandlerTests
{
    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_SemInspecaoNenhuma_StatusNunca()
    {
        var db = CriarDb(nameof(Handle_SemInspecaoNenhuma_StatusNunca));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        db.Obras.Add(obra);
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.Alojamentos.Add(alojamento);
        await db.SaveChangesAsync();

        var handler = new ListarAlojamentosQueryHandler(db);
        var resultado = await handler.Handle(new ListarAlojamentosQuery(obra.Id), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("nunca", dto.StatusUltimaInspecao);
        Assert.Equal(0, dto.TotalMoradores);
    }

    [Fact]
    public async Task Handle_UltimaInspecaoConcluidaDentroDoPrazo_StatusEmDia()
    {
        var db = CriarDb(nameof(Handle_UltimaInspecaoConcluidaDentroDoPrazo_StatusEmDia));
        var obra = new Obra { Codigo = "OB2", Nome = "Obra Teste 2" };
        db.Obras.Add(obra);
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        var alojamento = new Alojamento { Nome = "Alojamento 02", ObraId = obra.Id };
        db.Alojamentos.Add(alojamento);
        db.Inspecoes.Add(new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            AlojamentoId = alojamento.Id,
            ChecklistModeloId = Guid.NewGuid(),
            ResponsavelUsuarioId = Guid.NewGuid(),
            Status = StatusInspecao.Concluida,
            Data = DateTime.UtcNow.AddDays(-5),
            NumeroDocumento = "INSP-EM-DIA",
        });
        await db.SaveChangesAsync();

        var handler = new ListarAlojamentosQueryHandler(db);
        var resultado = await handler.Handle(new ListarAlojamentosQuery(obra.Id), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("em-dia", dto.StatusUltimaInspecao);
    }

    [Fact]
    public async Task Handle_UltimaInspecaoConcluidaForaDoPrazo_StatusAtrasada()
    {
        var db = CriarDb(nameof(Handle_UltimaInspecaoConcluidaForaDoPrazo_StatusAtrasada));
        var obra = new Obra { Codigo = "OB3", Nome = "Obra Teste 3" };
        db.Obras.Add(obra);
        db.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 30 });
        var alojamento = new Alojamento { Nome = "Alojamento 03", ObraId = obra.Id };
        db.Alojamentos.Add(alojamento);
        db.Inspecoes.Add(new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            AlojamentoId = alojamento.Id,
            ChecklistModeloId = Guid.NewGuid(),
            ResponsavelUsuarioId = Guid.NewGuid(),
            Status = StatusInspecao.Concluida,
            Data = DateTime.UtcNow.AddDays(-45),
            NumeroDocumento = "INSP-ATRASADA",
        });
        await db.SaveChangesAsync();

        var handler = new ListarAlojamentosQueryHandler(db);
        var resultado = await handler.Handle(new ListarAlojamentosQuery(obra.Id), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("atrasada", dto.StatusUltimaInspecao);
    }
}
