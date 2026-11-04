using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Application.Usuarios.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Usuarios;

// Prestador de serviço PJ (04/11/2026): usuário que assina e dá curso, mas não é efetivo de obra.
public class PrestadorServicoTests
{
    private static readonly ICpfHashService CpfHash = new ICpfHashServiceFake();

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, Usuario Usuario, Obra Obra, Funcao Funcao)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-1", Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Engenheiro de Segurança do Trabalho" };
        var usuario = new Usuario { Email = "engenheiro@exemplo.com", Nome = "Engenheiro PJ" };
        db.AddRange(obra, funcao, usuario);
        await db.SaveChangesAsync();
        return (db, usuario, obra, funcao);
    }

    [Fact]
    public async Task Cria_o_cadastro_de_prestador_e_vincula_o_usuario()
    {
        var c = await PrepararAsync();

        var id = await new CriarPrestadorUsuarioCommandHandler(c.Db)
            .Handle(new CriarPrestadorUsuarioCommand(c.Usuario.Id, "12345678901", c.Obra.Id, c.Funcao.Id), default);

        var prestador = await c.Db.Trabalhadores.SingleAsync(t => t.Id == id);
        Assert.Equal(TipoVinculo.PrestadorServico, prestador.Vinculo);
        Assert.Equal("Engenheiro PJ", prestador.Nome);
        Assert.Equal(id, (await c.Db.Usuarios.SingleAsync(u => u.Id == c.Usuario.Id)).TrabalhadorId);
    }

    [Fact]
    public async Task Usuario_ja_vinculado_nao_ganha_outro_cadastro()
    {
        var c = await PrepararAsync();
        var handler = new CriarPrestadorUsuarioCommandHandler(c.Db);
        await handler.Handle(new CriarPrestadorUsuarioCommand(c.Usuario.Id, "12345678901", c.Obra.Id, c.Funcao.Id), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarPrestadorUsuarioCommand(c.Usuario.Id, "10987654321", c.Obra.Id, c.Funcao.Id), default));
        Assert.Equal(1, await c.Db.Trabalhadores.CountAsync());
    }

    [Fact]
    public async Task Prestador_fica_fora_da_liberacao_para_trabalho()
    {
        var c = await PrepararAsync();
        await new CriarPrestadorUsuarioCommandHandler(c.Db)
            .Handle(new CriarPrestadorUsuarioCommand(c.Usuario.Id, "12345678901", c.Obra.Id, c.Funcao.Id), default);

        var r = await new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(), default);

        Assert.Equal(0, r.Ativos);
        Assert.Equal(0, r.SemAsoValido);
    }

    [Fact]
    public async Task Sync_do_GRH_com_o_mesmo_CPF_nao_altera_o_prestador()
    {
        var c = await PrepararAsync();
        const string cpf = "38062559890";
        var prestador = new Trabalhador
        {
            ObraId = c.Obra.Id, FuncaoId = c.Funcao.Id, Nome = "Engenheiro PJ", Cpf = cpf, CpfHash = CpfHash.CalcularHash(cpf),
            Vinculo = TipoVinculo.PrestadorServico, DataAdmissao = new DateTime(2026, 11, 4),
        };
        c.Db.Trabalhadores.Add(prestador);
        await c.Db.SaveChangesAsync();

        var id = await new SincronizarColaboradorGrhCommandHandler(c.Db, CpfHash).Handle(new SincronizarColaboradorGrhCommand(
            Cpf: cpf, Nome: "Outro Nome", Pis: null, Ctps: null, DataNascimento: null, NomeMae: null, Endereco: null,
            Municipio: null, Uf: null, Cep: null, Matricula: null, DataAdmissao: new DateTime(2020, 1, 1), DataDemissao: new DateTime(2021, 1, 1),
            CargoNome: "Pedreiro", CargoCboCodigo: null, Salario: null, Situacao: SituacaoTrabalhador.Desligado, ObraNome: "Obra Teste",
            DataFimExperiencia1: null, DataFimExperiencia2: null, TamanhoBlusaEpi: null, TamanhoCalcaEpi: null, TamanhoCalcadoEpi: null), default);

        var depois = await c.Db.Trabalhadores.IgnoreQueryFilters().SingleAsync(t => t.Id == prestador.Id);
        Assert.Equal(prestador.Id, id);
        Assert.Equal("Engenheiro PJ", depois.Nome);
        Assert.Equal(c.Funcao.Id, depois.FuncaoId);
        Assert.True(depois.Ativo);
        Assert.Null(depois.DataDemissao);
    }
}

file class ICpfHashServiceFake : ICpfHashService
{
    private readonly CpfHashService _real = new();
    public string CalcularHash(string cpfPlano) => _real.CalcularHash(cpfPlano);
}
