using AAHBRANT.SST.Application.Alertas.Commands;
using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Asos;

// Limpeza de ASO que sumiu do G-RH (caso Genyson, 10/2026): o sistema só criava/atualizava e deixava parado como
// "Pendente" o documento provisório que a origem tinha apagado. Quem controla a obra é avisado por e-mail e sininho.
public class ImportarAsoGrhCommandHandlerTests
{
    private sealed class ClienteFalso : IAsoGrhClient
    {
        private readonly Func<IReadOnlyList<AsoGrhDto>> _lista;
        public ClienteFalso(Func<IReadOnlyList<AsoGrhDto>> lista) => _lista = lista;
        public Task<IReadOnlyList<AsoGrhDto>> ListarTodosAsync(CancellationToken ct = default) => Task.FromResult(_lista());
    }

    private sealed class EmailFalso : IEmailService
    {
        public bool Falha { get; init; }
        public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = new();

        public Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
        {
            if (Falha) throw new InvalidOperationException("Graph fora do ar");
            Enviados.Add((para, assunto, corpoHtml));
            return Task.CompletedTask;
        }

        public Task EnviarAsync(string para, string assunto, string corpoHtml, IReadOnlyList<AnexoEmail> anexos, CancellationToken ct = default)
            => EnviarAsync(para, assunto, corpoHtml, ct);
    }

    private sealed record Cenario(SstDbContext Db, Obra Obra, Funcao Funcao, Trabalhador Pessoa);

    private static AsoGrhDto NaOrigem(string grhId) => new(grhId, "00000000000", null, new DateTime(2026, 7, 14), new DateTime(2027, 7, 14), "APTO", null, null);

    private static Aso NovoAso(Guid trabalhadorId, string? grhId, ResultadoAso resultado = ResultadoAso.Pendente, string? medico = null) => new()
    {
        TrabalhadorId = trabalhadorId, GrhAsoId = grhId, ResultadoStatus = resultado, MedicoNome = medico,
        Tipo = TipoExameAso.Periodico, DataExame = new DateTime(2026, 7, 14), DataValidade = new DateTime(2027, 7, 14),
    };

    private static async Task<Cenario> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Parque Roger", Codigo = "PR" };
        var funcao = new Funcao { Nome = "Eletricista" };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();
        var pessoa = NovaPessoa(obra, funcao, "Genyson Aleixo Peixoto");
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();
        return new Cenario(db, obra, funcao, pessoa);
    }

    private static Trabalhador NovaPessoa(Obra obra, Funcao funcao, string nome) => new()
    {
        Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = obra.Id, FuncaoId = funcao.Id,
        DataAdmissao = new DateTime(2026, 1, 1),
    };

    private static async Task<Usuario> NovoTecnicoAsync(SstDbContext db, Obra? obra, string email = "bruno@aahbrant.com",
        TipoPerfilAcesso perfil = TipoPerfilAcesso.TecnicoSeguranca)
    {
        var usuario = new Usuario { Nome = "Bruno", Email = email, Status = StatusUsuario.Ativo };
        var acesso = new PerfilAcesso { Nome = perfil.ToString(), Tipo = perfil };
        db.Usuarios.Add(usuario);
        db.PerfisAcesso.Add(acesso);
        await db.SaveChangesAsync();
        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario.Id, PerfilAcessoId = acesso.Id, ObraId = obra?.Id });
        await db.SaveChangesAsync();
        return usuario;
    }

    private static (ImportarAsoGrhCommandHandler Handler, MediatorSimulado Mediator) Criar(
        SstDbContext db, Func<IReadOnlyList<AsoGrhDto>> origem, EmailFalso? email = null)
    {
        var mediator = new MediatorSimulado(_ => Guid.Empty);
        return (new ImportarAsoGrhCommandHandler(new ClienteFalso(origem), mediator, db, email ?? new EmailFalso(),
            NullLogger<ImportarAsoGrhCommandHandler>.Instance), mediator);
    }

    private static Task<bool> Ativo(SstDbContext db, Guid id) => db.Asos.IgnoreQueryFilters().Where(a => a.Id == id).Select(a => a.Ativo).SingleAsync();

    [Fact]
    public async Task Handle_DesativaQualquerAsoQueSumiuDoGrh_ComOuSemResultado_NaoImportaOMedico()
    {
        var c = await PrepararAsync();
        var presente = NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto);
        var aptoQueSumiu = NovoAso(c.Pessoa.Id, "GRH-2", ResultadoAso.Apto);
        var inaptoQueSumiu = NovoAso(c.Pessoa.Id, "GRH-3", ResultadoAso.Inapto);
        var pendenteComMedico = NovoAso(c.Pessoa.Id, "GRH-4", ResultadoAso.Pendente, medico: "Dra. Ana");
        var pendenteSemMedico = NovoAso(c.Pessoa.Id, "GRH-5");
        c.Db.Asos.AddRange(presente, aptoQueSumiu, inaptoQueSumiu, pendenteComMedico, pendenteSemMedico);
        await c.Db.SaveChangesAsync();

        var r = await Criar(c.Db, () => new[] { NaOrigem("GRH-1") }).Handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(4, r.Desativados);
        Assert.Null(r.DesativacaoIgnorada);
        Assert.True(await Ativo(c.Db, presente.Id));
        foreach (var aso in new[] { aptoQueSumiu, inaptoQueSumiu, pendenteComMedico, pendenteSemMedico })
            Assert.False(await Ativo(c.Db, aso.Id));
        Assert.Equal(5, await c.Db.Asos.IgnoreQueryFilters().CountAsync()); // soft delete: nada some do banco
    }

    [Fact]
    public async Task Handle_NuncaMexeEmAsoLancadoNoSst()
    {
        var c = await PrepararAsync();
        var presente = NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto);
        var manual = NovoAso(c.Pessoa.Id, grhId: null);
        c.Db.Asos.AddRange(presente, manual);
        await c.Db.SaveChangesAsync();

        var r = await Criar(c.Db, () => new[] { NaOrigem("GRH-1") }).Handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(0, r.Desativados);
        Assert.True(await Ativo(c.Db, manual.Id));
    }

    [Fact]
    public async Task Handle_ListaVaziaDoGrhNaoDesativaNada()
    {
        var c = await PrepararAsync();
        var aso = NovoAso(c.Pessoa.Id, "GRH-1");
        c.Db.Asos.Add(aso);
        await c.Db.SaveChangesAsync();

        var r = await Criar(c.Db, () => Array.Empty<AsoGrhDto>()).Handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(0, r.Desativados);
        Assert.NotNull(r.DesativacaoIgnorada);
        Assert.True(await Ativo(c.Db, aso.Id));
    }

    [Fact]
    public async Task Handle_FalhaNaLeituraDoGrhPropagaENaoDesativaNada()
    {
        var c = await PrepararAsync();
        var aso = NovoAso(c.Pessoa.Id, "GRH-1");
        c.Db.Asos.Add(aso);
        await c.Db.SaveChangesAsync();

        var (handler, _) = Criar(c.Db, () => throw new InvalidOperationException("G-RH fora do ar"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ImportarAsoGrhCommand(), default));
        Assert.True(await Ativo(c.Db, aso.Id));
    }

    [Fact]
    public async Task Handle_SumicoEmMassaEhBarradoPorSeguranca()
    {
        var c = await PrepararAsync();
        var todos = Enumerable.Range(1, 12).Select(i => NovoAso(c.Pessoa.Id, $"GRH-{i}")).ToList();
        c.Db.Asos.AddRange(todos);
        await c.Db.SaveChangesAsync();
        var email = new EmailFalso();
        await NovoTecnicoAsync(c.Db, c.Obra);

        // Só 1 dos 12 continua na origem: 11 sumiriam de uma vez (limite 10).
        var (handler, mediator) = Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email);
        var r = await handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(0, r.Desativados);
        Assert.NotNull(r.DesativacaoIgnorada);
        Assert.Equal(12, await c.Db.Asos.CountAsync());
        Assert.Empty(email.Enviados);
        Assert.DoesNotContain(mediator.Requisicoes, x => x is CriarAlertaCommand);
    }

    [Fact]
    public async Task Handle_AvisaOTecnicoDaObraPorEmailESininho_ComDataEResultado()
    {
        var c = await PrepararAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), NovoAso(c.Pessoa.Id, "GRH-2", medico: "Dra. Ana"));
        await c.Db.SaveChangesAsync();
        var tecnico = await NovoTecnicoAsync(c.Db, c.Obra);
        var email = new EmailFalso();

        var (handler, mediator) = Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email);
        await handler.Handle(new ImportarAsoGrhCommand(), default);

        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("bruno@aahbrant.com", enviado.Para);
        Assert.Contains("Genyson Aleixo Peixoto", enviado.Corpo);
        Assert.Contains("14/07/2027", enviado.Corpo);
        Assert.Contains("Sem resultado", enviado.Corpo);
        Assert.DoesNotContain("Dra.", enviado.Corpo);

        var alerta = Assert.Single(mediator.Requisicoes.OfType<CriarAlertaCommand>());
        Assert.Equal(TipoAlerta.AsoDesativadoGrh, alerta.Tipo);
        Assert.Equal(tecnico.Id, alerta.DestinatarioUsuarioId);
        Assert.Equal(c.Obra.Id, alerta.ObraId);
    }

    [Fact]
    public async Task Handle_AvisoDizSeAPessoaContinuaComAsoValidoOuFicouBloqueada()
    {
        var c = await PrepararAsync();
        var outra = NovaPessoa(c.Obra, c.Funcao, "Josiran dos Anjos Silva");
        c.Db.Trabalhadores.Add(outra);
        await c.Db.SaveChangesAsync();
        // Genyson: o ASO que sumiu era um pendente, mas ele tem outro apto válido. Josiran: só tinha o que sumiu.
        c.Db.Asos.AddRange(
            NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), NovoAso(c.Pessoa.Id, "GRH-2"),
            NovoAso(outra.Id, "GRH-3", ResultadoAso.Apto));
        await c.Db.SaveChangesAsync();
        await NovoTecnicoAsync(c.Db, c.Obra);
        var email = new EmailFalso();

        var (handler, mediator) = Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email);
        await handler.Handle(new ImportarAsoGrhCommand(), default);

        var corpo = Assert.Single(email.Enviados).Corpo;
        Assert.Contains("Tem outro ASO válido", corpo);
        Assert.Contains("Sem ASO válido", corpo);
        Assert.Contains("1 pessoa(s) ficaram sem ASO apto válido", Assert.Single(mediator.Requisicoes.OfType<CriarAlertaCommand>()).Descricao);
    }

    [Fact]
    public async Task Handle_SoAvisaOTecnicoDaObraDaPessoa_NaoOutrosPerfisNemOutraObra()
    {
        var c = await PrepararAsync();
        var outraObra = new Obra { Nome = "Ponte Rio Cuiá", Codigo = "PRC" };
        c.Db.Obras.Add(outraObra);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), NovoAso(c.Pessoa.Id, "GRH-2"));
        await c.Db.SaveChangesAsync();
        await NovoTecnicoAsync(c.Db, c.Obra, "bruno@aahbrant.com");
        await NovoTecnicoAsync(c.Db, outraObra, "outro@aahbrant.com");
        await NovoTecnicoAsync(c.Db, c.Obra, "engenheiro@aahbrant.com", TipoPerfilAcesso.EngenheiroSeguranca);
        var email = new EmailFalso();

        await Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email).Handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(new[] { "bruno@aahbrant.com" }, email.Enviados.Select(e => e.Para));
    }

    [Fact]
    public async Task Handle_ObraSemTecnico_DesativaMesmoAssimEnaoAvisaNinguem()
    {
        var c = await PrepararAsync();
        var sumiu = NovoAso(c.Pessoa.Id, "GRH-2");
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), sumiu);
        await c.Db.SaveChangesAsync();
        var email = new EmailFalso();

        var (handler, mediator) = Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email);
        var r = await handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(1, r.Desativados);
        Assert.False(await Ativo(c.Db, sumiu.Id));
        Assert.Empty(email.Enviados);
        Assert.DoesNotContain(mediator.Requisicoes, x => x is CriarAlertaCommand);
    }

    [Fact]
    public async Task Handle_FalhaNoEmailNaoImpedeODesativarNemOSininho()
    {
        var c = await PrepararAsync();
        var sumiu = NovoAso(c.Pessoa.Id, "GRH-2");
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), sumiu);
        await c.Db.SaveChangesAsync();
        await NovoTecnicoAsync(c.Db, c.Obra);

        var (handler, mediator) = Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, new EmailFalso { Falha = true });
        var r = await handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Equal(1, r.Desativados);
        Assert.False(await Ativo(c.Db, sumiu.Id));
        Assert.Single(mediator.Requisicoes.OfType<CriarAlertaCommand>());
    }

    [Fact]
    public async Task Handle_PessoaDesligadaNaoGeraAviso()
    {
        var c = await PrepararAsync();
        c.Pessoa.DataDemissao = new DateTime(2026, 9, 1);
        c.Pessoa.Situacao = SituacaoTrabalhador.Desligado;
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, "GRH-1", ResultadoAso.Apto), NovoAso(c.Pessoa.Id, "GRH-2"));
        await c.Db.SaveChangesAsync();
        await NovoTecnicoAsync(c.Db, c.Obra);
        var email = new EmailFalso();

        await Criar(c.Db, () => new[] { NaOrigem("GRH-1") }, email).Handler.Handle(new ImportarAsoGrhCommand(), default);

        Assert.Empty(email.Enviados);
    }
}
