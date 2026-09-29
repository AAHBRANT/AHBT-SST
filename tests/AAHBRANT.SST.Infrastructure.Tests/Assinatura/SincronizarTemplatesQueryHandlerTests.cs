using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Tests.Assinatura;

public class SincronizarTemplatesQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveRetornarSoTemplatesDaMesmaObraDoDispositivo()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_DeveRetornarSoTemplatesDaMesmaObraDoDispositivo));

        var obraA = new Obra { Codigo = "OBR-009", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OBR-010", Nome = "Obra B" };
        db.Obras.AddRange(obraA, obraB);

        var trabalhadorA = new Trabalhador { ObraId = obraA.Id, Nome = "Trab A", Matricula = "M-006", Cpf = "12312312312" };
        var trabalhadorB = new Trabalhador { ObraId = obraB.Id, Nome = "Trab B", Matricula = "M-007", Cpf = "45645645645" };
        db.Trabalhadores.AddRange(trabalhadorA, trabalhadorB);

        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivoA = new DispositivoAgenteBiometrico
        {
            ObraId = obraA.Id,
            Nome = "Totem A",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivoA);
        await db.SaveChangesAsync();

        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhadorA.Id, TemplateCriptografado = "cifrado-a", CapturadoEm = DateTime.UtcNow });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = trabalhadorB.Id, TemplateCriptografado = "cifrado-b", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var handler = new SincronizarTemplatesQueryHandler(db, autenticador);

        var resultado = await handler.Handle(new SincronizarTemplatesQuery(dispositivoA.Id, segredo), CancellationToken.None);

        Assert.Single(resultado);
        Assert.Equal(trabalhadorA.Id, resultado[0].TrabalhadorId);
        Assert.Equal("cifrado-a", resultado[0].TemplateCriptografado);
    }

    [Fact]
    public async Task Handle_DeveAtualizarUltimaSincronizacao()
    {
        using var db = SstDbContextInMemoryTests.CriarContexto(nameof(Handle_DeveAtualizarUltimaSincronizacao));
        var obra = new Obra { Codigo = "OBR-011", Nome = "Obra Teste 11" };
        db.Obras.Add(obra);
        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem X",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();

        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var handler = new SincronizarTemplatesQueryHandler(db, autenticador);

        await handler.Handle(new SincronizarTemplatesQuery(dispositivo.Id, segredo), CancellationToken.None);

        var atualizado = await db.DispositivosAgenteBiometrico.FirstAsync(d => d.Id == dispositivo.Id);
        Assert.NotNull(atualizado.UltimaSincronizacaoEm);
    }

    // Regressao (29/09): o agente local chama a sincronizacao sem usuario (TemAcessoGlobal=false, nenhuma obra
    // permitida). O filtro global de RBAC do Trabalhador zerava a lista e o quiosque dizia "nenhum template".
    [Fact]
    public async Task Handle_DeveRetornarTemplates_QuandoNaoHaUsuarioAutenticado()
    {
        var usuarioAtual = new CurrentUserService();
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nameof(Handle_DeveRetornarTemplates_QuandoNaoHaUsuarioAutenticado))
            .Options;
        using var db = new SstDbContext(options, usuarioAtual);

        var obra = new Obra { Codigo = "OBR-012", Nome = "Obra Teste 12" };
        db.Obras.Add(obra);
        var ativo = new Trabalhador { ObraId = obra.Id, Nome = "Ativo", Matricula = "M-020", Cpf = "11111111111" };
        var inativo = new Trabalhador { ObraId = obra.Id, Nome = "Inativo", Matricula = "M-021", Cpf = "22222222222", Ativo = false };
        db.Trabalhadores.AddRange(ativo, inativo);
        var segredo = SegredoDispositivoHasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id,
            Nome = "Totem Y",
            SegredoHash = SegredoDispositivoHasher.GerarHash(segredo),
        };
        db.DispositivosAgenteBiometrico.Add(dispositivo);
        await db.SaveChangesAsync();
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = ativo.Id, TemplateCriptografado = "cifrado-ativo", CapturadoEm = DateTime.UtcNow });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = inativo.Id, TemplateCriptografado = "cifrado-inativo", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        usuarioAtual.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());

        var autenticador = new DispositivoAgenteAutenticador(db, new SegredoDispositivoHasherService());
        var handler = new SincronizarTemplatesQueryHandler(db, autenticador);

        var resultado = await handler.Handle(new SincronizarTemplatesQuery(dispositivo.Id, segredo), CancellationToken.None);

        var unico = Assert.Single(resultado);
        Assert.Equal(ativo.Id, unico.TrabalhadorId);
    }
}
