using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

public class ObterLiberacaoParaTrabalhoQueryHandlerTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    private sealed class Cenario
    {
        public required IAppDbContext Db { get; init; }
        public required Obra Obra { get; init; }
        public required Obra OutraObra { get; init; }
        public required Funcao Funcao { get; init; }
        public required CursoTreinamento Curso { get; init; }
        public required CatalogoEpi Epi { get; init; }
    }

    // Função "Pedreiro" que exige 1 curso e 1 EPI.
    private static async Task<Cenario> Preparar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var outraObra = new Obra { Nome = "Obra B", Codigo = "B" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var curso = new CursoTreinamento { Nome = "NR-35", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12 };
        db.Obras.AddRange(obra, outraObra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(curso);
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id });
        db.MatrizEpiFuncoes.Add(new MatrizEpiFuncao { FuncaoId = funcao.Id, CatalogoEpiId = epi.Id });
        await db.SaveChangesAsync();
        return new Cenario { Db = db, Obra = obra, OutraObra = outraObra, Funcao = funcao, Curso = curso, Epi = epi };
    }

    private static Trabalhador NovoTrabalhador(Funcao funcao, Obra obra, string nome,
        TipoVinculo vinculo = TipoVinculo.Clt, DateTime? demissao = null) => new()
    {
        Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = obra.Id, FuncaoId = funcao.Id,
        Vinculo = vinculo, DataAdmissao = Hoje.AddYears(-1), DataDemissao = demissao,
    };

    private static Aso AsoApto(Trabalhador t, int diasParaVencer, ResultadoAso resultado = ResultadoAso.Apto) => new()
    {
        TrabalhadorId = t.Id, Tipo = TipoExameAso.Periodico, DataExame = Hoje.AddDays(diasParaVencer).AddYears(-1),
        DataValidade = Hoje.AddDays(diasParaVencer), ResultadoStatus = resultado,
    };

    private static Treinamento Curso(Trabalhador t, CursoTreinamento curso, int diasParaVencer) => new()
    {
        TrabalhadorId = t.Id, CursoTreinamentoId = curso.Id, DataRealizacao = Hoje.AddDays(diasParaVencer).AddYears(-2),
        DataValidade = Hoje.AddDays(diasParaVencer),
    };

    private static EntregaEpi Entrega(Trabalhador t, CatalogoEpi epi, int? diasParaVencer = 100, bool confirmada = true) => new()
    {
        TrabalhadorId = t.Id, CatalogoEpiId = epi.Id, DataEntrega = Hoje.AddDays(-30), Confirmada = confirmada,
        DataValidade = diasParaVencer is { } d ? Hoje.AddDays(d) : null,
    };

    private static Task<LiberacaoTrabalhoDto> Consultar(IAppDbContext db, Guid? obraId = null)
        => new ObterLiberacaoParaTrabalhoQueryHandler(db).Handle(new ObterLiberacaoParaTrabalhoQuery(obraId), default);

    [Fact]
    public async Task Handle_TrabalhadorComTudoEmDiaEstaLiberado()
    {
        var c = await Preparar();
        var t = NovoTrabalhador(c.Funcao, c.Obra, "Em dia");
        c.Db.Trabalhadores.Add(t);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.Add(AsoApto(t, 200));
        c.Db.Treinamentos.Add(Curso(t, c.Curso, 300));
        c.Db.EntregasEpi.Add(Entrega(t, c.Epi));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c.Db);

        Assert.Equal(1, r.Ativos);
        Assert.Equal(1, r.Liberados);
        Assert.Equal(0, r.Bloqueados);
    }

    [Fact]
    public async Task Handle_CadaMotivoBloqueiaEEhContadoSeparadamente()
    {
        var c = await Preparar();
        var asoVencido = NovoTrabalhador(c.Funcao, c.Obra, "ASO vencido");
        var cursoVencido = NovoTrabalhador(c.Funcao, c.Obra, "Curso vencido");
        var epiVencido = NovoTrabalhador(c.Funcao, c.Obra, "EPI vencido");
        var epiNaoConfirmado = NovoTrabalhador(c.Funcao, c.Obra, "EPI reservado");
        c.Db.Trabalhadores.AddRange(asoVencido, cursoVencido, epiVencido, epiNaoConfirmado);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(AsoApto(asoVencido, -1), AsoApto(cursoVencido, 200), AsoApto(epiVencido, 200), AsoApto(epiNaoConfirmado, 200));
        c.Db.Treinamentos.AddRange(Curso(asoVencido, c.Curso, 300), Curso(cursoVencido, c.Curso, -5), Curso(epiVencido, c.Curso, 300), Curso(epiNaoConfirmado, c.Curso, 300));
        c.Db.EntregasEpi.AddRange(Entrega(asoVencido, c.Epi), Entrega(cursoVencido, c.Epi), Entrega(epiVencido, c.Epi, -2), Entrega(epiNaoConfirmado, c.Epi, 100, confirmada: false));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c.Db);

        Assert.Equal(4, r.Ativos);
        Assert.Equal(0, r.Liberados);
        Assert.Equal(1, r.SemAsoValido);
        Assert.Equal(1, r.TreinamentoPendente);
        Assert.Equal(2, r.EpiPendente);
    }

    [Fact]
    public async Task Handle_AsoComRestricaoDentroDaValidadeLibera_InaptoOuPendenteNao()
    {
        var c = await Preparar();
        var comRestricao = NovoTrabalhador(c.Funcao, c.Obra, "Com restrição");
        var inapto = NovoTrabalhador(c.Funcao, c.Obra, "Inapto");
        c.Db.Trabalhadores.AddRange(comRestricao, inapto);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(AsoApto(comRestricao, 100, ResultadoAso.AptoComRestricao), AsoApto(inapto, 100, ResultadoAso.Inapto));
        c.Db.Treinamentos.AddRange(Curso(comRestricao, c.Curso, 300), Curso(inapto, c.Curso, 300));
        c.Db.EntregasEpi.AddRange(Entrega(comRestricao, c.Epi), Entrega(inapto, c.Epi));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c.Db);

        Assert.Equal(1, r.Liberados);
        Assert.Equal(1, r.SemAsoValido);
    }

    [Fact]
    public async Task Handle_TecnicoDeSegurancaNaoPrecisaDeTreinamentoMasPrecisaDeAso()
    {
        var c = await Preparar();
        var tecnica = new Funcao { Nome = "Técnico de Segurança do Trabalho" };
        c.Db.Funcoes.Add(tecnica);
        await c.Db.SaveChangesAsync();
        c.Db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = tecnica.Id, CursoTreinamentoId = c.Curso.Id });
        var semAso = NovoTrabalhador(tecnica, c.Obra, "Técnico sem ASO");
        var comAso = NovoTrabalhador(tecnica, c.Obra, "Técnico com ASO");
        c.Db.Trabalhadores.AddRange(semAso, comAso);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.Add(AsoApto(comAso, 100));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c.Db);

        Assert.Equal(1, r.Liberados);
        Assert.Equal(1, r.SemAsoValido);
        Assert.Equal(0, r.TreinamentoPendente);
    }

    [Fact]
    public async Task Handle_IntegracaoDeSegurancaSoVaiParaTerceirizado()
    {
        var c = await Preparar();
        var integracao = new CursoTreinamento { Nome = "Integração", ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        c.Db.CursosTreinamento.Add(integracao);
        var clt = NovoTrabalhador(c.Funcao, c.Obra, "CLT");
        var terceirizado = NovoTrabalhador(c.Funcao, c.Obra, "Terceirizado", TipoVinculo.Terceirizado);
        c.Db.Trabalhadores.AddRange(clt, terceirizado);
        await c.Db.SaveChangesAsync();
        foreach (var t in new[] { clt, terceirizado })
        {
            c.Db.Asos.Add(AsoApto(t, 100));
            c.Db.Treinamentos.Add(Curso(t, c.Curso, 300));
            c.Db.EntregasEpi.Add(Entrega(t, c.Epi));
        }
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c.Db);

        Assert.Equal(1, r.Liberados);          // o CLT não precisa da Integração
        Assert.Equal(1, r.TreinamentoPendente); // o terceirizado ainda não fez
    }

    [Fact]
    public async Task Handle_IgnoraDesligadosEFiltraPorObra()
    {
        var c = await Preparar();
        var ativo = NovoTrabalhador(c.Funcao, c.Obra, "Ativo");
        var desligado = NovoTrabalhador(c.Funcao, c.Obra, "Desligado", demissao: Hoje.AddDays(-5));
        var deOutraObra = NovoTrabalhador(c.Funcao, c.OutraObra, "De outra obra");
        c.Db.Trabalhadores.AddRange(ativo, desligado, deOutraObra);
        await c.Db.SaveChangesAsync();

        var geral = await Consultar(c.Db);
        var daObra = await Consultar(c.Db, c.Obra.Id);

        Assert.Equal(2, geral.Ativos);
        Assert.Equal(1, daObra.Ativos);
        Assert.Equal(1, daObra.Bloqueados);
    }
}
