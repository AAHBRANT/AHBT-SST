using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ListarPendenciasTerceirizadoQueryHandlerTests
{
    static ListarPendenciasTerceirizadoQueryHandlerTests()
    {
        CpfCriptografiaContexto.Configurar(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_CenarioCompleto_RetornaAsTresListas()
    {
        var db = CriarDb(nameof(Handle_CenarioCompleto_RetornaAsTresListas));
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var contratoAtivo = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow), DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        var contratoEncerrado = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-002",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-8)), DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            GJuriContratoId = "gjuri-2", Status = StatusContrato.Encerrado,
        };
        db.Contratos.AddRange(contratoAtivo, contratoEncerrado);
        await db.SaveChangesAsync();

        db.Trabalhadores.AddRange(
            new Trabalhador
            {
                ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Pendente", Matricula = "MAT-1", Cpf = "52998224725",
                Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow, EmpresaId = empresa.Id, ContratoId = contratoAtivo.Id,
            },
            new Trabalhador
            {
                ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Ainda ativo no encerrado", Matricula = "MAT-2", Cpf = "11144477735",
                Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow, EmpresaId = empresa.Id, ContratoId = contratoEncerrado.Id,
            });
        await db.SaveChangesAsync();

        var catalogo = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.CatalogoEpis.Add(catalogo);
        await db.SaveChangesAsync();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.EpiEstoqueInsuficiente, Severidade = SeveridadeAlerta.Critico, Status = StatusAlerta.Aberto,
            Titulo = "Estoque insuficiente de Capacete", EntidadeOrigemTipo = "Trabalhador", EntidadeOrigemId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var handler = new ListarPendenciasTerceirizadoQueryHandler(db);
        var painel = await handler.Handle(new ListarPendenciasTerceirizadoQuery(), default);

        Assert.Equal(2, painel.PessoasBloqueadas.Count); // ambas sem ASO/integração cadastrados.
        Assert.Single(painel.ContratosEncerradosComPessoasAtivas);
        Assert.Equal("CT-002", painel.ContratosEncerradosComPessoasAtivas[0].NumeroContrato);
        Assert.Single(painel.AlertasEstoqueInsuficiente);
    }
}
