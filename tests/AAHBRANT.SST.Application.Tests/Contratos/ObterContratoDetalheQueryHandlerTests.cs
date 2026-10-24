using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Contratos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Contratos;

public class ObterContratoDetalheQueryHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_ContratoComVagas_RetornaDetalheComVagas()
    {
        var db = CriarDb(nameof(Handle_ContratoComVagas_RetornaDetalheComVagas));
        var obra = new Obra { Codigo = "OBRA1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1",
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        db.ContratoVagasFuncao.Add(new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 5, QuantidadePreenchidas = 2 });
        await db.SaveChangesAsync();

        var handler = new ObterContratoDetalheQueryHandler(db);
        var resultado = await handler.Handle(new ObterContratoDetalheQuery(contrato.Id), default);

        Assert.NotNull(resultado);
        Assert.Equal("XPTO", resultado!.EmpresaRazaoSocial);
        Assert.Single(resultado.Vagas);
        Assert.Equal(5, resultado.Vagas[0].QuantidadeVagas);
        Assert.Equal(2, resultado.Vagas[0].QuantidadePreenchidas);
    }

    [Fact]
    public async Task Handle_ContratoInexistente_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_ContratoInexistente_RetornaNull));
        var handler = new ObterContratoDetalheQueryHandler(db);

        var resultado = await handler.Handle(new ObterContratoDetalheQuery(Guid.NewGuid()), default);

        Assert.Null(resultado);
    }
}
