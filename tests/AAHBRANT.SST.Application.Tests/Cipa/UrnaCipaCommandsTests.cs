using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Cipa.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Cipa;

public class UrnaCipaCommandsTests
{
    private sealed record Cenario(IAppDbContext Db, CurrentUserService Usuario, ProcessoEleitoralCipa Processo,
        List<Trabalhador> Eleitores, CandidatoCipa CandidatoA, CandidatoCipa CandidatoB);

    private static async Task<Cenario> MontarAsync(string nome, int eleitores = 4)
    {
        var usuario = new CurrentUserService();
        usuario.DefinirUsuarioId(Guid.NewGuid());
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, usuario);

        var obra = new Obra { Nome = "Obra", Codigo = "OB", Status = StatusObra.EmAndamento };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        var lista = Enumerable.Range(0, eleitores).Select(i => new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = $"Eleitor {i}", Cpf = $"{i:00000000000}",
            Vinculo = TipoVinculo.Clt, DataAdmissao = DateTime.UtcNow.AddYears(-1),
        }).ToList();
        db.Trabalhadores.AddRange(lista);
        // Não votam: terceirizado e demitido.
        db.Trabalhadores.Add(new Trabalhador { ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Terceiro", Cpf = "99999999991", Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow });
        db.Trabalhadores.Add(new Trabalhador { ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "Demitido", Cpf = "99999999992", Vinculo = TipoVinculo.Clt, DataAdmissao = DateTime.UtcNow.AddYears(-2), DataDemissao = DateTime.UtcNow.AddDays(-3) });

        var processo = new ProcessoEleitoralCipa { ObraId = obra.Id, Status = StatusProcessoEleitoralCipa.InscricoesEncerradas, DataConvocacao = DateTime.UtcNow };
        db.ProcessosEleitoraisCipa.Add(processo);
        var a = new CandidatoCipa { ProcessoEleitoralId = processo.Id, TrabalhadorId = lista[0].Id, Status = StatusCandidatoCipa.Deferido };
        var b = new CandidatoCipa { ProcessoEleitoralId = processo.Id, TrabalhadorId = lista[1].Id, Status = StatusCandidatoCipa.Deferido };
        db.CandidatosCipa.AddRange(a, b);
        await db.SaveChangesAsync();
        return new Cenario(db, usuario, processo, lista, a, b);
    }

    private static Task<CedulaCipaDto> LiberarAsync(Cenario c, Trabalhador t) =>
        new LiberarEleitorMesarioCipaCommandHandler(c.Db, c.Usuario).Handle(new LiberarEleitorMesarioCipaCommand(c.Processo.Id, t.Id), default);

    private static Task<int> VotarAsync(Cenario c, CedulaCipaDto cedula, CandidatoCipa candidato) =>
        new VotarCipaCommandHandler(c.Db).Handle(new VotarCipaCommand(c.Processo.Id, cedula.CedulaId, candidato.Id), default);

    [Fact]
    public async Task Abrir_CongelaSoEmpregadosCltAtivos()
    {
        var c = await MontarAsync(nameof(Abrir_CongelaSoEmpregadosCltAtivos));
        var aptos = await new AbrirVotacaoCipaCommandHandler(c.Db).Handle(new AbrirVotacaoCipaCommand(c.Processo.Id), default);
        Assert.Equal(4, aptos);
        Assert.Equal(StatusProcessoEleitoralCipa.VotacaoAberta, c.Processo.Status);
    }

    [Fact]
    public async Task Votar_GravaComprovanteEVotoSemLigacao_EImpedeSegundoVoto()
    {
        var c = await MontarAsync(nameof(Votar_GravaComprovanteEVotoSemLigacao_EImpedeSegundoVoto));
        await new AbrirVotacaoCipaCommandHandler(c.Db).Handle(new AbrirVotacaoCipaCommand(c.Processo.Id), default);

        var cedula = await LiberarAsync(c, c.Eleitores[2]);
        Assert.Equal("Eleitor 2", cedula.EleitorNome);
        Assert.Equal("Pedreiro", cedula.EleitorFuncao);

        var total = await VotarAsync(c, cedula, c.CandidatoA);
        Assert.Equal(1, total);

        var voto = Assert.Single(c.Db.VotosCipa);
        Assert.Equal(c.CandidatoA.Id, voto.CandidatoCipaId);
        Assert.Null(typeof(VotoCipa).GetProperty("TrabalhadorId"));
        Assert.Null(typeof(VotoCipa).GetProperty("CreatedAtUtc"));

        var eleitor = Assert.Single(c.Db.EleitoresCipa);
        Assert.NotNull(eleitor.VotouEmUtc);
        Assert.Equal(0, eleitor.VotouEmUtc!.Value.Second);
        Assert.Equal(MetodoIdentificacaoEleitorCipa.Mesario, eleitor.Metodo);
        Assert.Equal(c.Usuario.UsuarioId, eleitor.LiberadoPorUsuarioId);

        await Assert.ThrowsAsync<JaVotouCipaException>(() => LiberarAsync(c, c.Eleitores[2]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => VotarAsync(c, cedula, c.CandidatoB));
        Assert.Single(c.Db.VotosCipa);
    }

    [Fact]
    public async Task Identificar_RecusaQuemNaoEhEmpregadoAtivoDaObra()
    {
        var c = await MontarAsync(nameof(Identificar_RecusaQuemNaoEhEmpregadoAtivoDaObra));
        await new AbrirVotacaoCipaCommandHandler(c.Db).Handle(new AbrirVotacaoCipaCommand(c.Processo.Id), default);
        var terceiro = await c.Db.Trabalhadores.SingleAsync(t => t.Nome == "Terceiro");
        await Assert.ThrowsAsync<InvalidOperationException>(() => LiberarAsync(c, terceiro));
    }

    [Fact]
    public async Task Votar_ComVotacaoFechada_Recusa()
    {
        var c = await MontarAsync(nameof(Votar_ComVotacaoFechada_Recusa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LiberarAsync(c, c.Eleitores[2]));
    }

    [Fact]
    public async Task Encerrar_SemQuorum_NaoApura_ComQuorum_ContaVotos()
    {
        var c = await MontarAsync(nameof(Encerrar_SemQuorum_NaoApura_ComQuorum_ContaVotos));
        await new AbrirVotacaoCipaCommandHandler(c.Db).Handle(new AbrirVotacaoCipaCommand(c.Processo.Id), default);
        var encerrar = new EncerrarVotacaoCipaCommandHandler(c.Db);

        await VotarAsync(c, await LiberarAsync(c, c.Eleitores[0]), c.CandidatoB);
        var semQuorum = await encerrar.Handle(new EncerrarVotacaoCipaCommand(c.Processo.Id), default);
        Assert.False(semQuorum.Apurado);
        Assert.Equal(StatusProcessoEleitoralCipa.VotacaoAberta, c.Processo.Status);

        // Enquanto aberta, a tela da urna não expõe votos por candidato.
        var urna = await new ObterUrnaCipaQueryHandler(c.Db).Handle(new ObterUrnaCipaQuery(c.Processo.Id), default);
        Assert.Equal(1, urna!.TotalVotantes);
        Assert.Equal(2, urna.Candidatos.Count);

        await VotarAsync(c, await LiberarAsync(c, c.Eleitores[1]), c.CandidatoB);
        var apurado = await encerrar.Handle(new EncerrarVotacaoCipaCommand(c.Processo.Id), default);
        Assert.True(apurado.Apurado);
        Assert.Equal(StatusProcessoEleitoralCipa.VotacaoRealizada, c.Processo.Status);
        Assert.Equal(2, c.CandidatoB.VotosRecebidos);
        Assert.Equal(0, c.CandidatoA.VotosRecebidos);
    }

    [Theory]
    [InlineData(1, 4, false)]
    [InlineData(2, 4, true)]
    [InlineData(3, 5, true)]
    [InlineData(2, 5, false)]
    [InlineData(0, 0, false)]
    public void Quorum_ExigeMetadeDosAptos(int votantes, int aptos, bool esperado)
        => Assert.Equal(esperado, EncerrarVotacaoCipaCommandHandler.AtingiuQuorum(votantes, aptos));
}
