using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados;
using AAHBRANT.SST.Application.Terceirizados.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ListarPessoasTerceirizadasQueryHandlerTests
{
    static ListarPessoasTerceirizadasQueryHandlerTests()
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

    private static async Task<(Empresa Empresa, Contrato Contrato, Funcao Funcao, CursoTreinamento Integracao, Trabalhador Pessoa)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(integracao);
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

        var pessoa = new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João", Matricula = "MAT-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow,
            EmpresaId = empresa.Id, ContratoId = contrato.Id,
        };
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();

        return (empresa, contrato, funcao, integracao, pessoa);
    }

    [Fact]
    public async Task Handle_SemAsoESemIntegracao_RetornaStatusPendenteComAsPendencias()
    {
        var db = CriarDb(nameof(Handle_SemAsoESemIntegracao_RetornaStatusPendenteComAsPendencias));
        var (empresa, _, _, _, pessoa) = await SemearAsync(db);
        var handler = new ListarPessoasTerceirizadasQueryHandler(db);

        var resultado = await handler.Handle(new ListarPessoasTerceirizadasQuery(empresa.Id, null), default);

        var dto = Assert.Single(resultado);
        Assert.Equal(pessoa.Id, dto.TrabalhadorId);
        Assert.Equal(StatusLiberacaoTerceirizado.Pendente, dto.Status);
        Assert.Contains("ASO válido pendente", dto.Pendencias);
        Assert.Contains("Integração de Segurança pendente", dto.Pendencias);
    }

    [Fact]
    public async Task Handle_ComAsoValidoEIntegracaoValida_SemMaisExigencias_RetornaLiberada()
    {
        var db = CriarDb(nameof(Handle_ComAsoValidoEIntegracaoValida_SemMaisExigencias_RetornaLiberada));
        var (empresa, _, _, integracao, pessoa) = await SemearAsync(db);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = pessoa.Id, Tipo = TipoExameAso.Admissional,
            DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11),
            ResultadoStatus = ResultadoAso.Apto,
        });
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = pessoa.Id, CursoTreinamentoId = integracao.Id,
            DataRealizacao = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), CargaHorariaRealizada = 4,
        });
        await db.SaveChangesAsync();

        var handler = new ListarPessoasTerceirizadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarPessoasTerceirizadasQuery(empresa.Id, null), default);

        var dto = Assert.Single(resultado);
        Assert.Equal(StatusLiberacaoTerceirizado.Liberada, dto.Status);
        Assert.Empty(dto.Pendencias);
    }

    [Fact]
    public async Task Handle_EpiObrigatorioConfirmadoMasDevolvido_NaoContaComoSatisfeito()
    {
        var db = CriarDb(nameof(Handle_EpiObrigatorioConfirmadoMasDevolvido_NaoContaComoSatisfeito));
        var (empresa, _, funcao, integracao, pessoa) = await SemearAsync(db);
        var catalogo = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.CatalogoEpis.Add(catalogo);
        await db.SaveChangesAsync();
        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = catalogo.Id });
        db.Asos.Add(new Aso
        {
            TrabalhadorId = pessoa.Id, Tipo = TipoExameAso.Admissional,
            DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), ResultadoStatus = ResultadoAso.Apto,
        });
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = pessoa.Id, CursoTreinamentoId = integracao.Id,
            DataRealizacao = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), CargaHorariaRealizada = 4,
        });
        // Confirmada=true, mas já devolvida — não deve mais satisfazer a exigência da função.
        db.EntregasEpi.Add(new EntregaEpi
        {
            TrabalhadorId = pessoa.Id, CatalogoEpiId = catalogo.Id, DataEntrega = DateTime.UtcNow.AddMonths(-2),
            Quantidade = 1, MotivoTipo = MotivoEntregaEpi.Inicial, Confirmada = true, DataDevolucao = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var handler = new ListarPessoasTerceirizadasQueryHandler(db);
        var resultado = await handler.Handle(new ListarPessoasTerceirizadasQuery(empresa.Id, null), default);

        var dto = Assert.Single(resultado);
        Assert.Equal(StatusLiberacaoTerceirizado.Pendente, dto.Status);
        Assert.Contains("EPI não reservado (sem estoque): Capacete", dto.Pendencias);
    }
}
