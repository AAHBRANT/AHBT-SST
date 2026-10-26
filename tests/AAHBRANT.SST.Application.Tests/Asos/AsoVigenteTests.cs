using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Asos;
using AAHBRANT.SST.Application.Asos.Queries;
using AAHBRANT.SST.Application.Elegibilidade.Rules;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Asos;

// Regra única do ASO vigente (26/10/2026): o exame mais recente manda, o ASO antigo vira "superado" e nunca aparece como
// vencido ou pendente, e Pendente nunca libera. Cobre o helper e cada ponto do backend que antes escolhia o ASO do seu jeito.
public class AsoVigenteTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    private static AsoResumo Resumo(int diasExame, int diasValidade, ResultadoAso resultado, Guid? trabalhador = null, Guid? id = null)
        => new(id ?? Guid.NewGuid(), trabalhador ?? Guid.Empty, Hoje.AddDays(diasExame), Hoje.AddDays(diasValidade), resultado);

    // ---------- helper ----------

    [Fact]
    public void Eleger_ExameMaisRecenteMandaMesmoComAptoAnteriorAindaValido()
    {
        var aptoAntigo = Resumo(-200, 150, ResultadoAso.Apto);
        var pendenteNovo = Resumo(-5, 360, ResultadoAso.Pendente);

        var vigente = AsoVigente.Eleger(new[] { aptoAntigo, pendenteNovo });

        Assert.Equal(pendenteNovo.Id, vigente!.Value.Id);
        Assert.Equal(SituacaoAso.SemParecer, AsoVigente.Classificar(vigente, Hoje));
        Assert.False(AsoVigente.EstaLiberado(vigente, Hoje));
    }

    [Fact]
    public void Eleger_AsoAntigoVencidoNaoImportaQuandoHaExameNovoApto()
    {
        var antigo = Resumo(-400, -35, ResultadoAso.Apto);
        var novo = Resumo(-10, 355, ResultadoAso.Apto);

        var vigente = AsoVigente.Eleger(new[] { antigo, novo });

        Assert.Equal(novo.Id, vigente!.Value.Id);
        Assert.Equal(SituacaoAso.Liberado, AsoVigente.Classificar(vigente, Hoje));
    }

    [Fact]
    public void Eleger_EmpateDeDataPrefereOQueTemParecerSobreOPendente_EmQualquerOrdem()
    {
        var apto = Resumo(-30, 335, ResultadoAso.Apto);
        var pendente = Resumo(-30, 335, ResultadoAso.Pendente);

        Assert.Equal(apto.Id, AsoVigente.Eleger(new[] { apto, pendente })!.Value.Id);
        Assert.Equal(apto.Id, AsoVigente.Eleger(new[] { pendente, apto })!.Value.Id);
    }

    [Fact]
    public void Eleger_EmpateEntreAptoEInaptoDoMesmoExameFicaComOInapto()
    {
        var apto = Resumo(-30, 335, ResultadoAso.Apto);
        var inapto = Resumo(-30, 335, ResultadoAso.Inapto);

        Assert.Equal(inapto.Id, AsoVigente.Eleger(new[] { apto, inapto })!.Value.Id);
        Assert.Equal(inapto.Id, AsoVigente.Eleger(new[] { inapto, apto })!.Value.Id);
    }

    [Fact]
    public void Eleger_EmpateTotalDesempataPeloIdSemDependerDaOrdem()
    {
        var a = Resumo(-30, 335, ResultadoAso.Apto, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var b = Resumo(-30, 335, ResultadoAso.Apto, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.Equal(AsoVigente.Eleger(new[] { a, b }), AsoVigente.Eleger(new[] { b, a }));
    }

    [Theory]
    [InlineData(ResultadoAso.Apto, 100, SituacaoAso.Liberado)]
    [InlineData(ResultadoAso.AptoComRestricao, 100, SituacaoAso.Liberado)]
    [InlineData(ResultadoAso.Pendente, 100, SituacaoAso.SemParecer)]
    [InlineData(ResultadoAso.Inapto, 100, SituacaoAso.Inapto)]
    [InlineData(ResultadoAso.Apto, -1, SituacaoAso.Vencido)]
    [InlineData(ResultadoAso.Pendente, -1, SituacaoAso.Vencido)]
    [InlineData(ResultadoAso.Inapto, -1, SituacaoAso.Vencido)]
    public void Classificar_VencimentoVenceQualquerParecer_EParecerSoDecideDentroDaValidade(ResultadoAso resultado, int diasValidade, SituacaoAso esperado)
        => Assert.Equal(esperado, AsoVigente.Classificar(Resumo(-20, diasValidade, resultado), Hoje));

    [Fact]
    public void Classificar_SemNenhumAsoEhSemAso() => Assert.Equal(SituacaoAso.SemAso, AsoVigente.Classificar(null, Hoje));

    [Fact]
    public void ValidadeQueVenceHojeAindaVale() => Assert.Equal(SituacaoAso.Liberado, AsoVigente.Classificar(Resumo(-300, 0, ResultadoAso.Apto), Hoje));

    [Theory]
    [InlineData(ResultadoAso.Apto, 100, "Apto")]
    [InlineData(ResultadoAso.AptoComRestricao, 100, "Apto com restrição")]
    [InlineData(ResultadoAso.Pendente, 100, "Pendente")]
    [InlineData(ResultadoAso.Inapto, 100, "Inapto")]
    [InlineData(ResultadoAso.Apto, -3, "ASO vencido")]
    public void RotuloAptidao_SegueOVigenteEAValidade(ResultadoAso resultado, int diasValidade, string esperado)
        => Assert.Equal(esperado, AsoVigente.RotuloAptidao(Resumo(-20, diasValidade, resultado), Hoje));

    [Fact]
    public void RotuloAptidao_SemAso() => Assert.Equal("Sem ASO", AsoVigente.RotuloAptidao(null, Hoje));

    // ---------- pontos do backend ----------

    private sealed record Cenario(SstDbContext Db, Trabalhador Pessoa);

    private static async Task<Cenario> PrepararAsync(string nome = "Genyson Aleixo Peixoto")
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Parque Roger", Codigo = "PR" };
        var funcao = new Funcao { Nome = "Técnico de Segurança do Trabalho" }; // isenta de treinamento: o teste isola o ASO
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();
        var pessoa = new Trabalhador
        {
            Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = obra.Id, FuncaoId = funcao.Id,
            DataAdmissao = new DateTime(2025, 1, 1),
        };
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();
        return new Cenario(db, pessoa);
    }

    private static Aso NovoAso(Guid trabalhadorId, int diasExame, int diasValidade, ResultadoAso resultado,
        TipoExameAso tipo = TipoExameAso.Periodico) => new()
    {
        TrabalhadorId = trabalhadorId, Tipo = tipo, ResultadoStatus = resultado,
        DataExame = Hoje.AddDays(diasExame), DataValidade = Hoje.AddDays(diasValidade),
    };

    [Fact]
    public async Task ListarAsos_MarcaOVigenteEOsSuperadosESaiNaOrdemSemEmpate()
    {
        var c = await PrepararAsync();
        var admissional = NovoAso(c.Pessoa.Id, -480, -115, ResultadoAso.Apto, TipoExameAso.Admissional);
        var periodico = NovoAso(c.Pessoa.Id, -110, 255, ResultadoAso.Apto);
        c.Db.Asos.AddRange(admissional, periodico);
        await c.Db.SaveChangesAsync();

        var lista = await new ListarAsosQueryHandler(c.Db).Handle(new ListarAsosQuery(), default);

        Assert.Equal(2, lista.Count);
        Assert.Equal(periodico.Id, lista[0].Id);
        Assert.True(lista[0].Vigente);
        Assert.Null(lista[0].SubstituidoPorDataExame);
        Assert.False(lista[1].Vigente);
        Assert.Equal(periodico.DataExame.Date, lista[1].SubstituidoPorDataExame!.Value.Date);
    }

    [Fact]
    public async Task ListarAsos_DuplicataApto_Pendente_DoMesmoExame_MarcaOAptoComoVigente()
    {
        var c = await PrepararAsync("Willms");
        var apto = NovoAso(c.Pessoa.Id, -230, 135, ResultadoAso.Apto);
        var pendente = NovoAso(c.Pessoa.Id, -230, 135, ResultadoAso.Pendente);
        c.Db.Asos.AddRange(pendente, apto);
        await c.Db.SaveChangesAsync();

        var lista = await new ListarAsosQueryHandler(c.Db).Handle(new ListarAsosQuery(), default);

        Assert.Equal(apto.Id, lista.Single(a => a.Vigente).Id);
        Assert.Equal(apto.Id, lista[0].Id);
    }

    [Fact]
    public async Task Liberacao_ExameNovoPendenteBloqueiaMesmoComAptoAnteriorValido_ContandoComoSemParecer()
    {
        var c = await PrepararAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, -200, 165, ResultadoAso.Apto), NovoAso(c.Pessoa.Id, -5, 360, ResultadoAso.Pendente));
        await c.Db.SaveChangesAsync();

        var r = await new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(), default);

        Assert.Equal(0, r.Liberados);
        Assert.Equal(1, r.AsoSemResultado);
        Assert.Equal(0, r.AsoVencido);
    }

    [Fact]
    public async Task Liberacao_AsoAntigoVencidoNaoContaQuandoHaExameNovoApto()
    {
        var c = await PrepararAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, -480, -115, ResultadoAso.Apto, TipoExameAso.Admissional), NovoAso(c.Pessoa.Id, -110, 255, ResultadoAso.Apto));
        await c.Db.SaveChangesAsync();

        var r = await new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(), default);

        Assert.Equal(1, r.Liberados);
        Assert.Equal(0, r.AsoVencido);
        Assert.Equal(0, r.SemAsoValido);
    }

    [Fact]
    public async Task Liberacao_QuebraOsBloqueadosPeloVigente()
    {
        var c = await PrepararAsync("Vencido");
        var inapto = new Trabalhador { Nome = "Inapto", Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = c.Pessoa.ObraId, FuncaoId = c.Pessoa.FuncaoId, DataAdmissao = new DateTime(2025, 1, 1) };
        var semAso = new Trabalhador { Nome = "Sem ASO", Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = c.Pessoa.ObraId, FuncaoId = c.Pessoa.FuncaoId, DataAdmissao = new DateTime(2025, 1, 1) };
        c.Db.Trabalhadores.AddRange(inapto, semAso);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, -400, -35, ResultadoAso.Apto), NovoAso(inapto.Id, -10, 355, ResultadoAso.Inapto));
        await c.Db.SaveChangesAsync();

        var r = await new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(), default);

        Assert.Equal(3, r.Ativos);
        Assert.Equal(0, r.Liberados);
        Assert.Equal(1, r.AsoVencido);
        Assert.Equal(1, r.AsoInapto);
        Assert.Equal(1, r.SemAso);
    }

    [Fact]
    public async Task Elegibilidade_ExameNovoPendenteNaoAtendeMesmoComAptoAnteriorValido()
    {
        var c = await PrepararAsync();
        c.Db.Asos.AddRange(NovoAso(c.Pessoa.Id, -200, 165, ResultadoAso.Apto), NovoAso(c.Pessoa.Id, -5, 360, ResultadoAso.Pendente));
        await c.Db.SaveChangesAsync();

        var r = await new AsoValidoRule(c.Db).AvaliarAsync(new EligibilityRequest { TrabalhadorId = c.Pessoa.Id, ObraId = c.Pessoa.ObraId });

        Assert.False(r.Atendido);
        Assert.Contains("sem parecer", r.Detalhe);
    }

    [Fact]
    public async Task Elegibilidade_AsoAntigoVencidoNaoAtrapalhaOExameNovoApto_EDuplicataPendenteNaoDecide()
    {
        var c = await PrepararAsync();
        c.Db.Asos.AddRange(
            NovoAso(c.Pessoa.Id, -480, -115, ResultadoAso.Apto, TipoExameAso.Admissional),
            NovoAso(c.Pessoa.Id, -110, 255, ResultadoAso.Pendente), NovoAso(c.Pessoa.Id, -110, 255, ResultadoAso.Apto));
        await c.Db.SaveChangesAsync();

        var r = await new AsoValidoRule(c.Db).AvaliarAsync(new EligibilityRequest { TrabalhadorId = c.Pessoa.Id, ObraId = c.Pessoa.ObraId });

        Assert.True(r.Atendido);
    }

    [Fact]
    public async Task AlertaDeAso_SoOVigenteGeraAlerta_OAsoAntigoVencidoFicaSubstituido()
    {
        var c = await PrepararAsync();
        var antigo = NovoAso(c.Pessoa.Id, -480, -115, ResultadoAso.Apto, TipoExameAso.Admissional);
        var novo = NovoAso(c.Pessoa.Id, -110, 255, ResultadoAso.Apto);
        c.Db.Asos.AddRange(antigo, novo);
        await c.Db.SaveChangesAsync();

        var itens = await new AsoAlertaProvider(c.Db).ObterItensAsync();

        Assert.True(itens.Single(i => i.EntidadeOrigemId == antigo.Id).Substituido);
        Assert.False(itens.Single(i => i.EntidadeOrigemId == novo.Id).Substituido);
    }

    [Fact]
    public async Task AlertaDeAso_EmpateDeExameNaoDependeDaOrdemDoBanco()
    {
        var c = await PrepararAsync();
        var apto = NovoAso(c.Pessoa.Id, -230, -5, ResultadoAso.Apto);
        var pendente = NovoAso(c.Pessoa.Id, -230, -5, ResultadoAso.Pendente);
        c.Db.Asos.AddRange(pendente, apto);
        await c.Db.SaveChangesAsync();

        var itens = await new AsoAlertaProvider(c.Db).ObterItensAsync();

        Assert.False(itens.Single(i => i.EntidadeOrigemId == apto.Id).Substituido);
        Assert.True(itens.Single(i => i.EntidadeOrigemId == pendente.Id).Substituido);
    }
}
