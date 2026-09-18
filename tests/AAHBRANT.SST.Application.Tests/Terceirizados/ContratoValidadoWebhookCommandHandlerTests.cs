using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ContratoValidadoWebhookCommandHandlerTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Obra Obra, Funcao Funcao)> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();
        return (obra, funcao);
    }

    private static ContratoValidadoWebhookCommand CriarCommand(
        Guid obraId, string funcaoNome, string gjuriContratoId, string cnpj = "12345678000199", int quantidadeVagas = 5) =>
        new(gjuriContratoId, "CT-001",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            obraId,
            new EmpresaWebhookDto("Construtora XPTO", "XPTO", cnpj, "Elétrica", "João", "11999990000", "joao@xpto.com"),
            new List<VagaFuncaoWebhookDto> { new(funcaoNome, quantidadeVagas) });

    [Fact]
    public async Task Handle_ContratoNovo_CriaEmpresaContratoEVagas()
    {
        var db = CriarDb(nameof(Handle_ContratoNovo_CriaEmpresaContratoEVagas));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-1"), default);

        var contrato = await db.Contratos.FirstAsync(c => c.Id == contratoId);
        Assert.Equal("CT-001", contrato.NumeroContrato);
        var empresa = await db.Empresas.FirstAsync(e => e.Id == contrato.EmpresaId);
        Assert.Equal("12345678000199", empresa.Cnpj);
        var vaga = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        Assert.Equal(funcao.Id, vaga.FuncaoId);
        Assert.Equal(5, vaga.QuantidadeVagas);
    }

    [Fact]
    public async Task Handle_CnpjJaCadastrado_ReaproveitaEmpresaExistente()
    {
        var db = CriarDb(nameof(Handle_CnpjJaCadastrado_ReaproveitaEmpresaExistente));
        var (obra, funcao) = await SemearAsync(db);
        var empresaExistente = new Empresa { RazaoSocial = "XPTO Já Cadastrada", Cnpj = "12345678000199" };
        db.Empresas.Add(empresaExistente);
        await db.SaveChangesAsync();
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-2"), default);

        var contrato = await db.Contratos.FirstAsync(c => c.Id == contratoId);
        Assert.Equal(empresaExistente.Id, contrato.EmpresaId);
        Assert.Equal(1, await db.Empresas.CountAsync());
    }

    [Fact]
    public async Task Handle_MesmoGJuriContratoIdChamadoDuasVezes_SemVagaPreenchida_AtualizaQuantidadeDeVagas()
    {
        var db = CriarDb(nameof(Handle_MesmoGJuriContratoIdChamadoDuasVezes_SemVagaPreenchida_AtualizaQuantidadeDeVagas));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);
        var primeiroId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-3", quantidadeVagas: 5), default);

        var segundoId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-3", quantidadeVagas: 8), default);

        Assert.Equal(primeiroId, segundoId);
        Assert.Equal(1, await db.Contratos.CountAsync());
        var vaga = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == primeiroId);
        Assert.Equal(8, vaga.QuantidadeVagas); // reenvio antes de qualquer vaga preenchida corrige o dado.
    }

    [Fact]
    public async Task Handle_MesmoGJuriContratoId_ComVagaJaPreenchida_IgnoraNovoPayload()
    {
        var db = CriarDb(nameof(Handle_MesmoGJuriContratoId_ComVagaJaPreenchida_IgnoraNovoPayload));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);
        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-5", quantidadeVagas: 5), default);
        var vaga = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        vaga.QuantidadePreenchidas = 1;
        await db.SaveChangesAsync();

        await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-5", quantidadeVagas: 99), default);

        var vagaAposReenvio = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        Assert.Equal(5, vagaAposReenvio.QuantidadeVagas); // já tem gente alocada — contrato passa a ser imutável.
    }

    [Fact]
    public async Task Handle_ObraInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_ObraInexistente_LancaKeyNotFoundException));
        var (_, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(CriarCommand(Guid.NewGuid(), funcao.Nome, "gjuri-4"), default));
    }

    [Fact]
    public async Task Handle_NomeDeFuncaoInexistente_LancaKeyNotFoundException()
    {
        var db = CriarDb(nameof(Handle_NomeDeFuncaoInexistente_LancaKeyNotFoundException));
        var (obra, _) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(CriarCommand(obra.Id, "Função Que Não Existe", "gjuri-6"), default));
    }

    [Fact]
    public async Task Handle_MesmoGJuriContratoId_ComVagaJaPreenchida_IgnoraMesmoComFuncaoNomeInexistenteNoReenvio()
    {
        // Contrato já travado (vaga com gente alocada) é imutável: o payload do reenvio deixa de
        // importar por completo, inclusive um FuncaoNome que não resolve mais (typo, função
        // renomeada do lado do G-Juri etc.) — a checagem de trava precisa vir antes da resolução
        // de nome de função, senão esse reenvio inofensivo derrubaria o no-op com
        // KeyNotFoundException. Regressão coberta pela revisão de 2026-09-18.
        var db = CriarDb(nameof(Handle_MesmoGJuriContratoId_ComVagaJaPreenchida_IgnoraMesmoComFuncaoNomeInexistenteNoReenvio));
        var (obra, funcao) = await SemearAsync(db);
        var handler = new ContratoValidadoWebhookCommandHandler(db);
        var contratoId = await handler.Handle(CriarCommand(obra.Id, funcao.Nome, "gjuri-7", quantidadeVagas: 5), default);
        var vaga = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        vaga.QuantidadePreenchidas = 1;
        await db.SaveChangesAsync();

        var idRetornado = await handler.Handle(
            CriarCommand(obra.Id, "Função Que Não Existe Mais", "gjuri-7", quantidadeVagas: 99), default);

        Assert.Equal(contratoId, idRetornado);
        var vagaAposReenvio = await db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == contratoId);
        Assert.Equal(5, vagaAposReenvio.QuantidadeVagas);
        Assert.Equal(funcao.Id, vagaAposReenvio.FuncaoId);
        Assert.Equal(1, await db.ContratoVagasFuncao.CountAsync(v => v.ContratoId == contratoId));
    }
}
