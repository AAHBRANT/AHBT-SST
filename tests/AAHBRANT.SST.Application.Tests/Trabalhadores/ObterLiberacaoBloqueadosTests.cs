using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

// Lista de quem está bloqueado no card "Podem trabalhar hoje" do Início, com o motivo de cada um (26/10/2026). Os motivos de
// ASO seguem o ASO vigente (regra única, ver AsoVigente); os de treinamento, a matriz da função.
public class ObterLiberacaoBloqueadosTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    private sealed record Cenario(SstDbContext Db, Obra Obra, Obra OutraObra, Funcao Funcao, CursoTreinamento Curso);

    private static async Task<Cenario> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Parque Roger", Codigo = "PR" };
        var outraObra = new Obra { Nome = "Beira Rio", Codigo = "BR" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        db.Obras.AddRange(obra, outraObra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, outraObra, funcao, curso);
    }

    private static Trabalhador Pessoa(Cenario c, string nome, Obra? obra = null) => new()
    {
        Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = (obra ?? c.Obra).Id, FuncaoId = c.Funcao.Id,
        DataAdmissao = Hoje.AddYears(-1),
    };

    private static Aso AsoDe(Trabalhador t, int diasExame, int diasValidade, ResultadoAso resultado) => new()
    {
        TrabalhadorId = t.Id, Tipo = TipoExameAso.Periodico, ResultadoStatus = resultado,
        DataExame = Hoje.AddDays(diasExame), DataValidade = Hoje.AddDays(diasValidade),
    };

    private static Treinamento CursoDe(Trabalhador t, CursoTreinamento curso, int diasValidade) => new()
    {
        TrabalhadorId = t.Id, CursoTreinamentoId = curso.Id, DataRealizacao = Hoje.AddDays(diasValidade).AddYears(-2),
        DataValidade = Hoje.AddDays(diasValidade),
    };

    private static Task<LiberacaoTrabalhoDto> Consultar(Cenario c, Guid? obraId = null)
        => new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(obraId), default);

    [Fact]
    public async Task ListaSoQuemEstaBloqueado_ComOMotivoDeCadaUm_EmOrdemAlfabetica()
    {
        var c = await PrepararAsync();
        var liberado = Pessoa(c, "Liberado");
        var asoVencido = Pessoa(c, "Aso vencido");
        var cursoVencido = Pessoa(c, "Curso vencido");
        var semNada = Pessoa(c, "Sem nada");
        c.Db.Trabalhadores.AddRange(liberado, asoVencido, cursoVencido, semNada);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(AsoDe(liberado, -10, 355, ResultadoAso.Apto), AsoDe(asoVencido, -400, -35, ResultadoAso.Apto), AsoDe(cursoVencido, -10, 355, ResultadoAso.Apto));
        c.Db.Treinamentos.AddRange(CursoDe(liberado, c.Curso, 300), CursoDe(asoVencido, c.Curso, 300), CursoDe(cursoVencido, c.Curso, -5));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c);

        Assert.Equal(new[] { "Aso vencido", "Curso vencido", "Sem nada" }, r.PessoasBloqueadas.Select(p => p.Nome));
        Assert.Equal(r.Bloqueados, r.PessoasBloqueadas.Count);

        var aso = r.PessoasBloqueadas.Single(p => p.Nome == "Aso vencido");
        Assert.Equal("Pedreiro", aso.Funcao);
        Assert.Equal("Parque Roger", aso.Obra);
        Assert.Equal(new[] { "Aso" }, aso.Motivos.Select(m => m.Tipo));
        Assert.Contains($"ASO vencido em {Hoje.AddDays(-35):dd/MM/yyyy}", aso.Motivos[0].Texto);

        var curso = r.PessoasBloqueadas.Single(p => p.Nome == "Curso vencido");
        Assert.Equal(new[] { "Treinamento" }, curso.Motivos.Select(m => m.Tipo));
        Assert.Contains($"NR-35 vencido em {Hoje.AddDays(-5):dd/MM/yyyy}", curso.Motivos[0].Texto);

        var nada = r.PessoasBloqueadas.Single(p => p.Nome == "Sem nada");
        Assert.Equal(new[] { "Sem ASO cadastrado", "NR-35 nunca feito" }, nada.Motivos.Select(m => m.Texto));
    }

    [Fact]
    public async Task ExameVigenteSemParecerBloqueiaEOMotivoNaoMencionaOAptoAntigo()
    {
        var c = await PrepararAsync();
        var pessoa = Pessoa(c, "Exame novo sem parecer");
        c.Db.Trabalhadores.Add(pessoa);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(AsoDe(pessoa, -200, 165, ResultadoAso.Apto), AsoDe(pessoa, -5, 360, ResultadoAso.Pendente));
        c.Db.Treinamentos.Add(CursoDe(pessoa, c.Curso, 300));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c);

        var p = Assert.Single(r.PessoasBloqueadas);
        var motivo = Assert.Single(p.Motivos);
        Assert.Equal("Aso", motivo.Tipo);
        Assert.Contains("sem parecer", motivo.Texto);
    }

    [Fact]
    public async Task AsoAntigoSuperadoNaoGeraMotivoNemBloqueio()
    {
        var c = await PrepararAsync();
        var pessoa = Pessoa(c, "Renovou");
        c.Db.Trabalhadores.Add(pessoa);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(AsoDe(pessoa, -480, -115, ResultadoAso.Apto), AsoDe(pessoa, -110, 255, ResultadoAso.Apto));
        c.Db.Treinamentos.Add(CursoDe(pessoa, c.Curso, 300));
        await c.Db.SaveChangesAsync();

        var r = await Consultar(c);

        Assert.Empty(r.PessoasBloqueadas);
        Assert.Equal(1, r.Liberados);
    }

    [Fact]
    public async Task AInaptoMostraOMotivo_EOFiltroDeObraRecortaALista()
    {
        var c = await PrepararAsync();
        var inapto = Pessoa(c, "Inapto");
        var deOutraObra = Pessoa(c, "De outra obra", c.OutraObra);
        c.Db.Trabalhadores.AddRange(inapto, deOutraObra);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.Add(AsoDe(inapto, -10, 355, ResultadoAso.Inapto));
        c.Db.Treinamentos.AddRange(CursoDe(inapto, c.Curso, 300), CursoDe(deOutraObra, c.Curso, 300));
        await c.Db.SaveChangesAsync();

        var geral = await Consultar(c);
        var daObra = await Consultar(c, c.Obra.Id);

        Assert.Equal(2, geral.PessoasBloqueadas.Count);
        var p = Assert.Single(daObra.PessoasBloqueadas);
        Assert.Equal("Inapto", p.Nome);
        Assert.Equal("ASO inapto", p.Motivos[0].Texto);
    }
}
