using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.CursosObrigatorios;
using AAHBRANT.SST.Application.CursosTreinamento.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.CursosObrigatorios;

public class CursosObrigatoriosTests
{
    private sealed record Cenario(IAppDbContext Db, Obra Obra, Funcao Armador, Funcao Servente, CursoTreinamento Nr35, CursoTreinamento Nr06);

    // Obra com um GHE: Armador e Servente; a atividade de armação tem o perigo "Queda de altura".
    private static async Task<Cenario> Preparar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var armador = new Funcao { Nome = "Armador" };
        var servente = new Funcao { Nome = "Servente" };
        var nr35 = new CursoTreinamento { Nome = "Trabalho em Altura", NormaReferencia = "NR 35", ValidadeEmMeses = 24 };
        var nr06 = new CursoTreinamento { Nome = "Uso de EPI", NormaReferencia = "NR-06", ValidadeEmMeses = 12 };
        db.Obras.Add(obra);
        db.Funcoes.AddRange(armador, servente);
        db.CursosTreinamento.AddRange(nr35, nr06);
        var ghe = new Ghe { ObraId = obra.Id, Numero = 2, Setor = "Estrutura" };
        ghe.Funcoes.Add(new GheFuncao { FuncaoId = armador.Id });
        ghe.Funcoes.Add(new GheFuncao { FuncaoId = servente.Id });
        db.Ghes.Add(ghe);
        var atividade = new Atividade { ObraId = obra.Id, GheId = ghe.Id, Nome = "3.2 Montagem de armação de pilares" };
        db.Atividades.Add(atividade);
        var perigo = new Perigo { Nome = "Queda de altura" };
        db.Perigos.Add(perigo);
        db.Riscos.Add(new Risco { AtividadeId = atividade.Id, PerigoId = perigo.Id, Probabilidade = 3, Severidade = 4 });
        db.RegrasCursoObrigatorio.AddRange(
            new RegraCursoObrigatorio { Norma = "NR-06", Descricao = "EPI", AplicaTodasFuncoes = true },
            new RegraCursoObrigatorio { Norma = "NR-35", Descricao = "Altura", PalavrasChave = "altura, andaime" },
            new RegraCursoObrigatorio { Norma = "NR-33", Descricao = "Confinado", PalavrasChave = "armação" });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, armador, servente, nr35, nr06);
    }

    private static Task<ResultadoGeracaoCursosObrigatoriosDto> Gerar(Cenario c) =>
        new GerarCursosObrigatoriosObraCommandHandler(c.Db).Handle(new GerarCursosObrigatoriosObraCommand(c.Obra.Id), default);

    [Fact]
    public async Task Gerar_SugereCursoPeloPerigoDoPgrEPelaRegraDeTodasAsFuncoes()
    {
        var c = await Preparar();

        var resultado = await Gerar(c);

        var itens = await c.Db.CursosObrigatoriosFuncaoObra.ToListAsync();
        // 2 funções × (NR-06 + NR-35, achado pelo perigo do GHE). NR-33 casa pela atividade, mas não tem curso.
        Assert.Equal(4, resultado.Sugeridos);
        Assert.Equal(["NR-33"], resultado.NormasSemCurso);
        Assert.All(itens, i => Assert.Equal(StatusCursoObrigatorio.Sugerido, i.Status));
        var altura = itens.Single(i => i.FuncaoId == c.Armador.Id && i.CursoTreinamentoId == c.Nr35.Id);
        Assert.Contains("Queda de altura", altura.Motivo);
    }

    [Fact]
    public async Task Gerar_DeNovo_NaoDuplicaNemRessugereRecusado()
    {
        var c = await Preparar();
        await Gerar(c);
        var item = await c.Db.CursosObrigatoriosFuncaoObra.FirstAsync(i => i.FuncaoId == c.Servente.Id && i.CursoTreinamentoId == c.Nr35.Id);
        await new DecidirCursoObrigatorioCommandHandler(c.Db)
            .Handle(new DecidirCursoObrigatorioCommand(item.Id, false, "Servente não sobe", null, "Eng. Teste", false), default);

        var resultado = await Gerar(c);

        Assert.Equal(0, resultado.Sugeridos);
        Assert.Equal(4, await c.Db.CursosObrigatoriosFuncaoObra.CountAsync());
        var recusado = await c.Db.CursosObrigatoriosFuncaoObra.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(StatusCursoObrigatorio.Recusado, recusado.Status);
        Assert.Equal("Servente não sobe", recusado.JustificativaRecusa);
    }

    // ---- Quem pode decidir (17/11/2026): tecnico OU engenheiro, so nas obras em que tem o perfil ----

    private static async Task<Guid> Vincular(Cenario c, TipoPerfilAcesso tipo, Guid? obra)
    {
        var usuario = new Usuario { Nome = "Tecnico Beltrano", Email = $"{Guid.NewGuid()}@x.com", Status = StatusUsuario.Ativo };
        c.Db.Usuarios.Add(usuario);
        var perfil = new PerfilAcesso { Tipo = tipo, Nome = tipo.ToString(), EhSistema = true };
        c.Db.PerfisAcesso.Add(perfil);
        await c.Db.SaveChangesAsync();
        c.Db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario.Id, PerfilAcessoId = perfil.Id, ObraId = obra });
        await c.Db.SaveChangesAsync();
        return usuario.Id;
    }

    private static Task<int> AprovarTodas(Cenario c, Guid? usuario, Guid obraId) =>
        new AprovarSugestoesCursosObrigatoriosCommandHandler(c.Db)
            .Handle(new AprovarSugestoesCursosObrigatoriosCommand(obraId, usuario, "token", ExigirPerfilNaObra: true), default);

    [Theory]
    [InlineData(TipoPerfilAcesso.TecnicoSeguranca)]
    [InlineData(TipoPerfilAcesso.EngenheiroSeguranca)]
    public async Task Decidir_TecnicoOuEngenheiroDaObra_AprovaEGravaOProprioNome(TipoPerfilAcesso tipo)
    {
        var c = await Preparar();
        await Gerar(c);
        var usuario = await Vincular(c, tipo, c.Obra.Id);
        var item = await c.Db.CursosObrigatoriosFuncaoObra.FirstAsync();

        await new DecidirCursoObrigatorioCommandHandler(c.Db)
            .Handle(new DecidirCursoObrigatorioCommand(item.Id, true, null, usuario, "token", true), default);

        var salvo = await c.Db.CursosObrigatoriosFuncaoObra.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(StatusCursoObrigatorio.Aprovado, salvo.Status);
        Assert.Equal(usuario, salvo.DecididoPorUsuarioId);
        Assert.Equal("Tecnico Beltrano", salvo.DecididoPorNome);
    }

    [Fact]
    public async Task Decidir_TecnicoDeOutraObra_NaoAprovaItemDestaObra()
    {
        var c = await Preparar();
        await Gerar(c);
        var outra = new Obra { Nome = "Obra B", Codigo = "B" };
        c.Db.Obras.Add(outra);
        await c.Db.SaveChangesAsync();
        var tecnicoB = await Vincular(c, TipoPerfilAcesso.TecnicoSeguranca, outra.Id);
        var item = await c.Db.CursosObrigatoriosFuncaoObra.FirstAsync();

        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() =>
            new DecidirCursoObrigatorioCommandHandler(c.Db)
                .Handle(new DecidirCursoObrigatorioCommand(item.Id, true, null, tecnicoB, "token", true), default));

        Assert.All(await c.Db.CursosObrigatoriosFuncaoObra.ToListAsync(), i => Assert.Equal(StatusCursoObrigatorio.Sugerido, i.Status));
    }

    [Fact]
    public async Task Decidir_AdministradorGlobalOuDaObraAprova_AdministradorDeOutraObraNegado()
    {
        var c = await Preparar();
        await Gerar(c);
        var outra = new Obra { Nome = "Obra B", Codigo = "B" };
        c.Db.Obras.Add(outra);
        await c.Db.SaveChangesAsync();
        var adminGlobal = await Vincular(c, TipoPerfilAcesso.Administrador, null);
        var adminB = await Vincular(c, TipoPerfilAcesso.Administrador, outra.Id);
        var itens = await c.Db.CursosObrigatoriosFuncaoObra.ToListAsync();

        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() =>
            new DecidirCursoObrigatorioCommandHandler(c.Db)
                .Handle(new DecidirCursoObrigatorioCommand(itens[0].Id, true, null, adminB, "token", true), default));
        await new DecidirCursoObrigatorioCommandHandler(c.Db)
            .Handle(new DecidirCursoObrigatorioCommand(itens[0].Id, true, null, adminGlobal, "token", true), default);
        Assert.Equal(StatusCursoObrigatorio.Aprovado, (await c.Db.CursosObrigatoriosFuncaoObra.SingleAsync(i => i.Id == itens[0].Id)).Status);
    }

    [Fact]
    public async Task Decidir_UsuarioSemPerfilDeSegurancaOuSemUsuario_Negado()
    {
        var c = await Preparar();
        await Gerar(c);
        var gestor = await Vincular(c, TipoPerfilAcesso.GestorDeObra, c.Obra.Id);
        var item = await c.Db.CursosObrigatoriosFuncaoObra.FirstAsync();

        foreach (var quem in new Guid?[] { gestor, null })
            await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() =>
                new DecidirCursoObrigatorioCommandHandler(c.Db)
                    .Handle(new DecidirCursoObrigatorioCommand(item.Id, true, null, quem, "token", true), default));
    }

    [Fact]
    public async Task AprovarSugestoes_TecnicoGlobalAprovaTudo_TecnicoDeOutraObraNegado()
    {
        var c = await Preparar();
        await Gerar(c);
        var outra = new Obra { Nome = "Obra B", Codigo = "B" };
        c.Db.Obras.Add(outra);
        await c.Db.SaveChangesAsync();
        var tecnicoB = await Vincular(c, TipoPerfilAcesso.TecnicoSeguranca, outra.Id);
        var global = await Vincular(c, TipoPerfilAcesso.TecnicoSeguranca, null);

        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() => AprovarTodas(c, tecnicoB, c.Obra.Id));
        Assert.Equal(4, await AprovarTodas(c, global, c.Obra.Id));
    }

    [Fact]
    public async Task Adicionar_TecnicoDaObraAdiciona_TecnicoDeOutraObraNegado_SemExigirNaoChecaPerfil()
    {
        var c = await Preparar();
        var tecnicoA = await Vincular(c, TipoPerfilAcesso.TecnicoSeguranca, c.Obra.Id);
        var outra = new Obra { Nome = "Obra B", Codigo = "B" };
        c.Db.Obras.Add(outra);
        await c.Db.SaveChangesAsync();
        var tecnicoB = await Vincular(c, TipoPerfilAcesso.TecnicoSeguranca, outra.Id);
        var h = new AdicionarCursoObrigatorioCommandHandler(c.Db);

        await Assert.ThrowsAsync<AAHBRANT.SST.Application.Common.Seguranca.AcessoNegadoException>(() =>
            h.Handle(new AdicionarCursoObrigatorioCommand(c.Obra.Id, c.Armador.Id, c.Nr35.Id, tecnicoB, "t", true), default));
        await h.Handle(new AdicionarCursoObrigatorioCommand(c.Obra.Id, c.Armador.Id, c.Nr35.Id, tecnicoA, "t", true), default);
        var item = await c.Db.CursosObrigatoriosFuncaoObra.SingleAsync();
        Assert.Equal(StatusCursoObrigatorio.Aprovado, item.Status);
        Assert.Equal("Tecnico Beltrano", item.DecididoPorNome);
    }

    [Fact]
    public async Task Aptidao_ObraComCursosAprovados_UsaOsAprovadosEmVezDaMatrizGlobal()
    {
        var c = await Preparar();
        // Matriz global exigiria só NR-06; na obra o engenheiro aprovou só NR-35 para o Armador.
        c.Db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = c.Armador.Id, CursoTreinamentoId = c.Nr06.Id });
        c.Db.Trabalhadores.Add(new Trabalhador
        {
            Nome = "Zé", Cpf = "12345678901", ObraId = c.Obra.Id, FuncaoId = c.Armador.Id, DataAdmissao = DateTime.UtcNow.AddYears(-1),
        });
        await c.Db.SaveChangesAsync();
        await new AdicionarCursoObrigatorioCommandHandler(c.Db)
            .Handle(new AdicionarCursoObrigatorioCommand(c.Obra.Id, c.Armador.Id, c.Nr35.Id, null, "Eng. Teste", false), default);

        var aptidao = await new ObterAptidaoPorCursoQueryHandler(c.Db).Handle(new ObterAptidaoPorCursoQuery(c.Obra.Id), default);

        var item = Assert.Single(aptidao);
        Assert.Equal(c.Nr35.Id, item.CursoId);
        Assert.Equal(1, item.SemCurso);
    }
}
