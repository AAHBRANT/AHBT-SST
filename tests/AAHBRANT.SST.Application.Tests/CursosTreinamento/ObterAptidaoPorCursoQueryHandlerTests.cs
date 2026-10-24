using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.CursosTreinamento.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Tests.CursosTreinamento;

public class ObterAptidaoPorCursoQueryHandlerTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    private static async Task<(Funcao Funcao, CursoTreinamento Curso, Obra Obra, Obra OutraObra, IAppDbContext Db)> Preparar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var outraObra = new Obra { Nome = "Obra B", Codigo = "B" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        db.Obras.AddRange(obra, outraObra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id });
        await db.SaveChangesAsync();
        return (funcao, curso, obra, outraObra, db);
    }

    private static Trabalhador NovoTrabalhador(Funcao funcao, Obra obra, string nome, DateTime? demissao = null) => new()
    {
        Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = obra.Id, FuncaoId = funcao.Id,
        DataAdmissao = Hoje.AddYears(-1), DataDemissao = demissao,
    };

    private static Treinamento Registro(Trabalhador t, CursoTreinamento curso, DateTime validade) => new()
    {
        TrabalhadorId = t.Id, CursoTreinamentoId = curso.Id, DataRealizacao = validade.AddYears(-2), DataValidade = validade,
    };

    [Fact]
    public async Task Handle_ClassificaCadaTrabalhadorEmUmaFaixa()
    {
        var (funcao, curso, obra, _, db) = await Preparar();
        var emDia = NovoTrabalhador(funcao, obra, "Em dia");
        var vence = NovoTrabalhador(funcao, obra, "Vence");
        var vencido = NovoTrabalhador(funcao, obra, "Vencido");
        var sem = NovoTrabalhador(funcao, obra, "Sem curso");
        db.Trabalhadores.AddRange(emDia, vence, vencido, sem);
        await db.SaveChangesAsync();
        db.Treinamentos.AddRange(
            Registro(emDia, curso, Hoje.AddDays(120)),
            Registro(vence, curso, Hoje.AddDays(10)),
            Registro(vencido, curso, Hoje.AddDays(-1)));
        await db.SaveChangesAsync();

        var resultado = await new ObterAptidaoPorCursoQueryHandler(db).Handle(new ObterAptidaoPorCursoQuery(), default);

        var item = Assert.Single(resultado);
        Assert.Equal(4, item.Exigidos);
        Assert.Equal(1, item.EmDia);
        Assert.Equal(1, item.VencemEm30Dias);
        Assert.Equal(1, item.Vencidos);
        Assert.Equal(1, item.SemCurso);
    }

    [Fact]
    public async Task Handle_RenovacaoValidaSubstituiORegistroVencidoAntigo()
    {
        var (funcao, curso, obra, _, db) = await Preparar();
        var trabalhador = NovoTrabalhador(funcao, obra, "Renovou");
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        db.Treinamentos.AddRange(Registro(trabalhador, curso, Hoje.AddDays(-400)), Registro(trabalhador, curso, Hoje.AddDays(300)));
        await db.SaveChangesAsync();

        var item = Assert.Single(await new ObterAptidaoPorCursoQueryHandler(db).Handle(new ObterAptidaoPorCursoQuery(), default));

        Assert.Equal(1, item.EmDia);
        Assert.Equal(0, item.Vencidos);
    }

    [Fact]
    public async Task Handle_IgnoraDesligadosETecnicoDeSegurancaEFiltraPorObra()
    {
        var (funcao, curso, obra, outraObra, db) = await Preparar();
        var tecnico = new Funcao { Nome = "Técnico de Segurança do Trabalho" };
        db.Funcoes.Add(tecnico);
        await db.SaveChangesAsync();
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = tecnico.Id, CursoTreinamentoId = curso.Id });
        db.Trabalhadores.AddRange(
            NovoTrabalhador(funcao, obra, "Ativo"),
            NovoTrabalhador(funcao, obra, "Desligado", Hoje.AddDays(-5)),
            NovoTrabalhador(tecnico, obra, "Técnico"),
            NovoTrabalhador(funcao, outraObra, "De outra obra"));
        await db.SaveChangesAsync();

        var geral = Assert.Single(await new ObterAptidaoPorCursoQueryHandler(db).Handle(new ObterAptidaoPorCursoQuery(), default));
        var daObra = Assert.Single(await new ObterAptidaoPorCursoQueryHandler(db).Handle(new ObterAptidaoPorCursoQuery(obra.Id), default));

        Assert.Equal(2, geral.Exigidos); // ativo da obra A + o da obra B; sem desligado nem técnico
        Assert.Equal(1, daObra.Exigidos);
    }

    [Fact]
    public async Task Handle_CursoSemNenhumaFuncaoExigindoNaoApareceNaLista()
    {
        var (_, _, _, _, db) = await Preparar();

        var resultado = await new ObterAptidaoPorCursoQueryHandler(db).Handle(new ObterAptidaoPorCursoQuery(), default);

        Assert.Empty(resultado);
    }
}
