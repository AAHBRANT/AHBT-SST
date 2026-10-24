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

public class ContratoEncerradoWebhookCommandHandlerTests
{
    // O conversor de criptografia do CPF (Trabalhador.Cpf) exige chaves configuradas em
    // CpfCriptografiaContexto — normalmente feito por DependencyInjection.AddInfrastructure a partir
    // de appsettings, que este projeto de testes não executa. Configura uma chave só para o processo
    // de teste, mesmo padrão de CadastrarPessoaTerceirizadaCommandHandlerTests/
    // ResolverTrabalhadorPublicoQueryTests.
    static ContratoEncerradoWebhookCommandHandlerTests()
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

    private static async Task<Contrato> SemearContratoComPessoaAtivaAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
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
            GJuriContratoId = "gjuri-1", Status = StatusContrato.Validado,
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();

        db.Trabalhadores.Add(new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João", Matricula = "MAT-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow,
            EmpresaId = empresa.Id, ContratoId = contrato.Id,
        });
        await db.SaveChangesAsync();

        return contrato;
    }

    [Fact]
    public async Task Handle_ComPessoasAtivas_MarcaEncerradoEAlertaTecnicos()
    {
        var db = CriarDb(nameof(Handle_ComPessoasAtivas_MarcaEncerradoEAlertaTecnicos));
        var contrato = await SemearContratoComPessoaAtivaAsync(db);
        var tecnicoId = Guid.NewGuid();
        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { tecnicoId } };
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = new ContratoEncerradoWebhookCommandHandler(db, tecnicos, fila);

        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        var contratoAtualizado = await db.Contratos.FirstAsync(c => c.Id == contrato.Id);
        Assert.Equal(StatusContrato.Encerrado, contratoAtualizado.Status);
        var alerta = await db.Alertas.SingleAsync(a => a.EntidadeOrigemId == contrato.Id);
        Assert.Equal(TipoAlerta.ContratoTerceirizadoEncerrado, alerta.Tipo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
        Assert.Single(fila.Mensagens);

        var pessoa = await db.Trabalhadores.FirstAsync(t => t.ContratoId == contrato.Id);
        Assert.Null(pessoa.DataDemissao); // spec: só alerta, não desliga ninguém.
    }

    [Fact]
    public async Task Handle_ChamadoDuasVezes_EhIdempotente_NaoDuplicaAlerta()
    {
        var db = CriarDb(nameof(Handle_ChamadoDuasVezes_EhIdempotente_NaoDuplicaAlerta));
        var contrato = await SemearContratoComPessoaAtivaAsync(db);
        var tecnicos = new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = new List<Guid> { Guid.NewGuid() } };
        var handler = new ContratoEncerradoWebhookCommandHandler(db, tecnicos, new FilaNotificacaoTeamsFalsa());
        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        await handler.Handle(new ContratoEncerradoWebhookCommand(contrato.GJuriContratoId, DateOnly.FromDateTime(DateTime.UtcNow)), default);

        Assert.Equal(1, await db.Alertas.CountAsync(a => a.EntidadeOrigemId == contrato.Id));
    }

    [Fact]
    public async Task Handle_ContratoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ContratoInexistente_LancaKeyNotFoundException));
        var handler = new ContratoEncerradoWebhookCommandHandler(
            db, new TecnicosSegurancaPorObraServiceFalso(), new FilaNotificacaoTeamsFalsa());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ContratoEncerradoWebhookCommand("inexistente", DateOnly.FromDateTime(DateTime.UtcNow)), default));
    }
}
