using AAHBRANT.SST.Application.Contratos.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Contratos;

public class ContratoManualCommandsTests
{
    static ContratoManualCommandsTests()
    {
        ChavesCpfDeTeste.Configurar();
    }

    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Cenario(SstDbContext Db, CurrentUserService Usuario, Obra Obra, Empresa Empresa, Funcao Pedreiro, Funcao Eletricista);

    private static async Task<Cenario> SemearAsync(string nomeBanco)
    {
        var usuario = new CurrentUserService();
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, usuario);
        var obra = new Obra { Codigo = "O1", Nome = "Obra Centro" };
        var empresa = new Empresa { RazaoSocial = "Empreiteira XPTO", Cnpj = "12345678000199" };
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        var eletricista = new Funcao { Nome = "Eletricista" };
        db.AddRange(obra, empresa, pedreiro, eletricista);
        await db.SaveChangesAsync();
        return new Cenario(db, usuario, obra, empresa, pedreiro, eletricista);
    }

    private static CriarContratoCommand ComandoCriar(Cenario c, params VagaContratoInput[] vagas) => new(
        c.Empresa.Id, c.Obra.Id, "CT-001", Hoje, Hoje.AddMonths(6),
        vagas.Length > 0 ? vagas.ToList() : new List<VagaContratoInput> { new(c.Pedreiro.Id, 5) });

    private static async Task<Guid> CriarAsync(Cenario c, params VagaContratoInput[] vagas) =>
        await new CriarContratoCommandHandler(c.Db).Handle(ComandoCriar(c, vagas), default);

    [Fact]
    public async Task Criar_GeraContratoManualValidadoComAsVagas()
    {
        var c = await SemearAsync(nameof(Criar_GeraContratoManualValidadoComAsVagas));

        var id = await CriarAsync(c, new VagaContratoInput(c.Pedreiro.Id, 5), new VagaContratoInput(c.Eletricista.Id, 2));

        var contrato = await c.Db.Contratos.SingleAsync(x => x.Id == id);
        Assert.Equal(StatusContrato.Validado, contrato.Status);
        Assert.Null(contrato.GJuriContratoId);
        Assert.False(contrato.VeioDoGJuri);
        var vagas = await c.Db.ContratoVagasFuncao.Where(v => v.ContratoId == id).ToListAsync();
        Assert.Equal(2, vagas.Count);
        Assert.Equal(5, vagas.Single(v => v.FuncaoId == c.Pedreiro.Id).QuantidadeVagas);
    }

    [Fact]
    public async Task Criar_NumeroRepetidoNaMesmaEmpresa_ERecusado()
    {
        var c = await SemearAsync(nameof(Criar_NumeroRepetidoNaMesmaEmpresa_ERecusado));
        await CriarAsync(c);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CriarAsync(c));
    }

    [Fact]
    public async Task Criar_EmObraForaDoEscopoDoTecnico_NaoEncontraAObra()
    {
        var c = await SemearAsync(nameof(Criar_EmObraForaDoEscopoDoTecnico_NaoEncontraAObra));
        c.Usuario.DefinirEscopo(temAcessoGlobal: false, new[] { Guid.NewGuid() });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CriarAsync(c));
    }

    [Fact]
    public async Task Criar_EmpresaInativa_ERecusado()
    {
        var c = await SemearAsync(nameof(Criar_EmpresaInativa_ERecusado));
        c.Empresa.Status = StatusEmpresa.Inativa;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CriarAsync(c));
    }

    [Fact]
    public void Validador_FimAntesDoInicioOuFuncaoRepetida_EInvalido()
    {
        var validador = new CriarContratoCommandValidator();
        var funcao = Guid.NewGuid();
        var baseValida = new CriarContratoCommand(Guid.NewGuid(), Guid.NewGuid(), "CT-1", Hoje, Hoje.AddDays(30),
            new List<VagaContratoInput> { new(funcao, 1) });

        Assert.True(validador.Validate(baseValida).IsValid);
        Assert.False(validador.Validate(baseValida with { DataFimVigencia = Hoje.AddDays(-1) }).IsValid);
        Assert.False(validador.Validate(baseValida with { Vagas = new() { new(funcao, 1), new(funcao, 2) } }).IsValid);
        Assert.False(validador.Validate(baseValida with { Vagas = new() }).IsValid);
    }

    [Fact]
    public async Task Atualizar_AjustaVagasEAdicionaFuncaoNova()
    {
        var c = await SemearAsync(nameof(Atualizar_AjustaVagasEAdicionaFuncaoNova));
        var id = await CriarAsync(c);

        await new AtualizarContratoCommandHandler(c.Db).Handle(new AtualizarContratoCommand(
            id, "CT-001-A", Hoje, Hoje.AddMonths(12),
            new List<VagaContratoInput> { new(c.Pedreiro.Id, 8), new(c.Eletricista.Id, 3) }), default);

        var contrato = await c.Db.Contratos.SingleAsync(x => x.Id == id);
        Assert.Equal("CT-001-A", contrato.NumeroContrato);
        var vagas = await c.Db.ContratoVagasFuncao.Where(v => v.ContratoId == id).ToListAsync();
        Assert.Equal(8, vagas.Single(v => v.FuncaoId == c.Pedreiro.Id).QuantidadeVagas);
        Assert.Equal(3, vagas.Single(v => v.FuncaoId == c.Eletricista.Id).QuantidadeVagas);
    }

    [Fact]
    public async Task Atualizar_NaoDeixaVagasAbaixoDasPessoasJaCadastradas()
    {
        var c = await SemearAsync(nameof(Atualizar_NaoDeixaVagasAbaixoDasPessoasJaCadastradas));
        var id = await CriarAsync(c);
        var vaga = await c.Db.ContratoVagasFuncao.SingleAsync(v => v.ContratoId == id);
        vaga.QuantidadePreenchidas = 3;
        await c.Db.SaveChangesAsync();
        var handler = new AtualizarContratoCommandHandler(c.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AtualizarContratoCommand(
            id, "CT-001", Hoje, Hoje.AddMonths(6), new List<VagaContratoInput> { new(c.Pedreiro.Id, 2) }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AtualizarContratoCommand(
            id, "CT-001", Hoje, Hoje.AddMonths(6), new List<VagaContratoInput> { new(c.Eletricista.Id, 2) }), default));
    }

    [Fact]
    public async Task Atualizar_FuncaoRemovidaEDepoisDevolvida_ReativaAMesmaVaga()
    {
        var c = await SemearAsync(nameof(Atualizar_FuncaoRemovidaEDepoisDevolvida_ReativaAMesmaVaga));
        var id = await CriarAsync(c, new VagaContratoInput(c.Pedreiro.Id, 5), new VagaContratoInput(c.Eletricista.Id, 2));
        var handler = new AtualizarContratoCommandHandler(c.Db);

        await handler.Handle(new AtualizarContratoCommand(id, "CT-001", Hoje, Hoje.AddMonths(6),
            new List<VagaContratoInput> { new(c.Pedreiro.Id, 5) }), default);
        Assert.Single(await c.Db.ContratoVagasFuncao.Where(v => v.ContratoId == id).ToListAsync());

        await handler.Handle(new AtualizarContratoCommand(id, "CT-001", Hoje, Hoje.AddMonths(6),
            new List<VagaContratoInput> { new(c.Pedreiro.Id, 5), new(c.Eletricista.Id, 4) }), default);

        var todas = await c.Db.ContratoVagasFuncao.IgnoreQueryFilters().Where(v => v.ContratoId == id).ToListAsync();
        Assert.Equal(2, todas.Count);
        Assert.Equal(4, todas.Single(v => v.FuncaoId == c.Eletricista.Id && v.Ativo).QuantidadeVagas);
    }

    [Fact]
    public async Task AtualizarEEncerrar_ContratoDoGJuri_SaoRecusados()
    {
        var c = await SemearAsync(nameof(AtualizarEEncerrar_ContratoDoGJuri_SaoRecusados));
        var contrato = new Contrato
        {
            EmpresaId = c.Empresa.Id, ObraId = c.Obra.Id, NumeroContrato = "GJ-1", GJuriContratoId = "gjuri-1",
            DataInicioVigencia = Hoje, DataFimVigencia = Hoje.AddMonths(6),
        };
        c.Db.Contratos.Add(contrato);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AtualizarContratoCommandHandler(c.Db).Handle(
            new AtualizarContratoCommand(contrato.Id, "GJ-1", Hoje, Hoje.AddMonths(6), new() { new(c.Pedreiro.Id, 1) }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EncerrarContratoCommandHandler(c.Db).Handle(
            new EncerrarContratoCommand(contrato.Id, Hoje), default));
    }

    [Fact]
    public async Task Encerrar_MudaStatusEDevolveQuantasPessoasSeguemAtivas()
    {
        var c = await SemearAsync(nameof(Encerrar_MudaStatusEDevolveQuantasPessoasSeguemAtivas));
        var id = await CriarAsync(c);
        c.Db.Trabalhadores.Add(new Trabalhador
        {
            ObraId = c.Obra.Id, FuncaoId = c.Pedreiro.Id, Nome = "João", Matricula = "T-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, EmpresaId = c.Empresa.Id, ContratoId = id, DataAdmissao = DateTime.UtcNow,
        });
        await c.Db.SaveChangesAsync();

        var ativas = await new EncerrarContratoCommandHandler(c.Db).Handle(new EncerrarContratoCommand(id, Hoje), default);

        Assert.Equal(1, ativas);
        var contrato = await c.Db.Contratos.SingleAsync(x => x.Id == id);
        Assert.Equal(StatusContrato.Encerrado, contrato.Status);
        Assert.Equal(Hoje, contrato.DataEncerramento);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EncerrarContratoCommandHandler(c.Db)
            .Handle(new EncerrarContratoCommand(id, Hoje), default));
    }
}
