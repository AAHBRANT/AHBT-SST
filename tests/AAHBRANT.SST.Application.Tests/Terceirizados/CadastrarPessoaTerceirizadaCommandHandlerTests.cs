using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using AAHBRANT.SST.Application.Tests.TestSupport;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class CadastrarPessoaTerceirizadaCommandHandlerTests
{
    // O conversor de criptografia do CPF (Trabalhador.Cpf) exige chaves configuradas em
    // CpfCriptografiaContexto — normalmente feito por DependencyInjection.AddInfrastructure a partir
    // de appsettings, que este projeto de testes não executa. Configura uma chave só para o processo
    // de teste, mesmo padrão de ExportarFichaEpiTrabalhadorQueryHandlerTests/
    // ResolverTrabalhadorPublicoQueryTests.
    static CadastrarPessoaTerceirizadaCommandHandlerTests()
    {
        ChavesCpfDeTeste.Configurar();
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Empresa Empresa, Funcao Funcao, Contrato Contrato, ContratoVagaFuncao Vaga, CatalogoEpi Epi)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();

        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = epi.Id });
        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        var vaga = new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 2, QuantidadePreenchidas = 0 };
        db.ContratoVagasFuncao.Add(vaga);
        await db.SaveChangesAsync();

        return (obra, empresa, funcao, contrato, vaga, epi);
    }

    private static CadastrarPessoaTerceirizadaCommandHandler CriarHandler(
        IAppDbContext db, TecnicosSegurancaPorObraServiceFalso? tecnicos = null, FilaNotificacaoTeamsFalsa? fila = null) =>
        new(db, tecnicos ?? new TecnicosSegurancaPorObraServiceFalso(), fila ?? new FilaNotificacaoTeamsFalsa());

    [Fact]
    public async Task Handle_ComEstoqueSuficiente_CriaTrabalhadorEEntregaEpiPendente()
    {
        var db = CriarDb(nameof(Handle_ComEstoqueSuficiente_CriaTrabalhadorEEntregaEpiPendente));
        var (obra, _, funcao, contrato, vaga, epi) = await SemearAsync(db);
        db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = epi.Id, ObraId = obra.Id, Saldo = 5 });
        await db.SaveChangesAsync();

        var handler = CriarHandler(db);

        var trabalhadorId = await handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "João Pedreiro", "MAT-1", "52998224725", DateTime.UtcNow),
            default);

        var trabalhador = await db.Trabalhadores.FirstAsync(t => t.Id == trabalhadorId);
        Assert.Equal(TipoVinculo.Terceirizado, trabalhador.Vinculo);
        Assert.Equal(contrato.EmpresaId, trabalhador.EmpresaId);
        Assert.Equal(contrato.Id, trabalhador.ContratoId);

        var vagaAtualizada = await db.ContratoVagasFuncao.FirstAsync(v => v.Id == vaga.Id);
        Assert.Equal(1, vagaAtualizada.QuantidadePreenchidas);

        var entrega = await db.EntregasEpi.SingleAsync(e => e.TrabalhadorId == trabalhadorId);
        Assert.False(entrega.Confirmada);

        var estoqueAtualizado = await db.EstoquesEpi.FirstAsync(e => e.CatalogoEpiId == epi.Id && e.ObraId == obra.Id);
        Assert.Equal(4, estoqueAtualizado.Saldo);
    }

    [Fact]
    public async Task Handle_SemEstoque_NaoCriaEntregaEAlertaTecnicos()
    {
        var db = CriarDb(nameof(Handle_SemEstoque_NaoCriaEntregaEAlertaTecnicos));
        var (_, _, funcao, contrato, _, epi) = await SemearAsync(db);
        var tecnicoId = Guid.NewGuid();

        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { tecnicoId } };
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = CriarHandler(db, tecnicos, fila);

        var trabalhadorId = await handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "Maria Pedreira", "MAT-2", "11144477735", DateTime.UtcNow),
            default);

        Assert.False(await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId));
        var alerta = await db.Alertas.SingleAsync(a => a.TrabalhadorId == trabalhadorId);
        Assert.Equal(TipoAlerta.EpiEstoqueInsuficiente, alerta.Tipo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
        Assert.Single(fila.Mensagens);
    }

    [Fact]
    public async Task Handle_VagaSemDisponibilidade_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_VagaSemDisponibilidade_LancaInvalidOperationException));
        var (_, _, funcao, contrato, vaga, _) = await SemearAsync(db);
        vaga.QuantidadePreenchidas = vaga.QuantidadeVagas;
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "X", "MAT-3", "52998224725", DateTime.UtcNow), default));
    }

    [Fact]
    public async Task Handle_ContratoNaoValidado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_ContratoNaoValidado_LancaInvalidOperationException));
        var (_, _, funcao, contrato, _, _) = await SemearAsync(db);
        var contratoEntidade = await db.Contratos.FirstAsync(c => c.Id == contrato.Id);
        contratoEntidade.Status = StatusContrato.Encerrado;
        await db.SaveChangesAsync();
        var handler = CriarHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "X", "MAT-4", "52998224725", DateTime.UtcNow), default));
    }

    [Fact]
    public async Task Handle_EpiComCaVencidoMesmoComEstoque_NaoReservaEGeraAlerta()
    {
        var db = CriarDb(nameof(Handle_EpiComCaVencidoMesmoComEstoque_NaoReservaEGeraAlerta));
        var (obra, _, funcao, contrato, _, epi) = await SemearAsync(db);

        var catalogoEntidade = await db.CatalogoEpis.FirstAsync(c => c.Id == epi.Id);
        catalogoEntidade.CertificadoAprovacaoValidade = DateTime.UtcNow.AddDays(-1);
        db.EstoquesEpi.Add(new EstoqueEpi { CatalogoEpiId = epi.Id, ObraId = obra.Id, Saldo = 5 });
        await db.SaveChangesAsync();

        var tecnicoId = Guid.NewGuid();
        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { tecnicoId } };
        var handler = CriarHandler(db, tecnicos);

        var trabalhadorId = await handler.Handle(
            new CadastrarPessoaTerceirizadaCommand(contrato.Id, funcao.Id, "Carlos Pedreiro", "MAT-5", "11144477735", DateTime.UtcNow),
            default);

        Assert.False(await db.EntregasEpi.AnyAsync(e => e.TrabalhadorId == trabalhadorId));

        var estoqueInalterado = await db.EstoquesEpi.FirstAsync(e => e.CatalogoEpiId == epi.Id && e.ObraId == obra.Id);
        Assert.Equal(5, estoqueInalterado.Saldo);

        var alerta = await db.Alertas.SingleAsync(a => a.TrabalhadorId == trabalhadorId);
        Assert.Equal(TipoAlerta.EpiEstoqueInsuficiente, alerta.Tipo);
        Assert.Contains("CA vencido", alerta.Titulo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
    }
}
