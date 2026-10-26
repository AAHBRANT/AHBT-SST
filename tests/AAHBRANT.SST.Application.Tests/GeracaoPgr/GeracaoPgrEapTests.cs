using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Gerador de PGR pela EAP (21/10/2026): IA sugere, servidor confere, Engenheiro de Segurança aprova.
public class GeracaoPgrEapTests
{
    private sealed class IaFalsa : IGeradorPgrIa
    {
        public List<ItemEapGerado> Itens { get; set; } = new();
        public List<GheGerado> Ghes { get; set; } = new();
        public List<RiscoGerado> Riscos { get; set; } = new();
        public ContextoRiscosAtividade? ContextoRiscos { get; private set; }

        public Task<List<ItemEapGerado>> LerEapAsync(string textoEap, CancellationToken ct) => Task.FromResult(Itens);
        public Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct) => Task.FromResult(Ghes);
        public Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade contexto, CancellationToken ct)
        {
            ContextoRiscos = contexto;
            return Task.FromResult(Riscos);
        }
    }

    private sealed class ExtratorFalso : IExtratorTextoEap
    {
        public string Extrair(byte[] conteudo, string nomeArquivo, string contentType) => "1 | Estrutura\n1.1 | Concretagem";
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Obra Obra, Funcao Armador)> CriarAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        var armador = new Funcao { Nome = "ARMADOR" };
        var matriz = new MatrizRiscoConfig { Nome = "5x5", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        for (var p = 1; p <= 5; p++)
            for (var s = 1; s <= 5; s++)
                matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = p, Severidade = s, NivelRisco = p * s >= 20 ? NivelRisco.Critico : p * s >= 10 ? NivelRisco.Alto : NivelRisco.Baixo });
        db.AddRange(obra, armador, matriz,
            new RequisitoLegal { Norma = "NR-35", Artigo = "35.1", Titulo = "Altura", Descricao = "x", Status = StatusRequisitoLegal.Ativo },
            new RequisitoLegal { Norma = "NR-12", Artigo = "12.1", Titulo = "Máquinas", Descricao = "x", Status = StatusRequisitoLegal.EmRevisao });
        await db.SaveChangesAsync();
        return (db, obra, armador);
    }

    private static async Task<Guid> CriarGeracaoAsync(Infrastructure.Persistencia.SstDbContext db, Obra obra, IaFalsa ia)
    {
        ia.Itens = new()
        {
            new ItemEapGerado { Codigo = "1", Nome = "Estrutura", Nivel = 1 },
            new ItemEapGerado { Codigo = "1.1", Nome = "Concretagem de laje", Nivel = 2 },
            new ItemEapGerado { Codigo = "9", Nome = "Medições", Nivel = 2, Usar = false },
        };
        return await new CriarGeracaoPgrEapCommandHandler(db, new ExtratorFalso(), ia).Handle(new CriarGeracaoPgrEapCommand(
            obra.Id, "PGR Obra Teste", new DateTime(2026, 10, 21), null, "eap.xlsx", "application/x", new byte[] { 1 }, null, "Téc. Ana"), default);
    }

    [Fact]
    public async Task SugerirGhes_CasaFuncaoDoCadastro_ENumera_EDescartaAtividadeQueNaoEstaNaEap()
    {
        var (db, obra, armador) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new()
        {
            new GheGerado
            {
                Nome = "Estrutura",
                Funcoes = new() { new FuncaoGerada { Nome = "Armador", Descricao = "Monta armaduras" }, new FuncaoGerada { Nome = "Soldador" } },
                Atividades = new() { new AtividadeGerada { CodigoEap = "1.1", Nome = "nome da IA" }, new AtividadeGerada { CodigoEap = "7.7" } },
            },
        };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        var ghe = Assert.Single(dto.Estrutura.Ghes);
        Assert.Equal(1, ghe.Numero);
        Assert.Equal(armador.Id, ghe.Funcoes.Single(f => f.Nome == "Armador").FuncaoId);
        Assert.Null(ghe.Funcoes.Single(f => f.Nome == "Soldador").FuncaoId);
        var atividade = Assert.Single(ghe.Atividades);
        Assert.Equal("Concretagem de laje", atividade.Nome); // nome vem da EAP, não da IA
        Assert.Contains(dto.PendenciasAprovacao, p => p.Contains("Soldador sem descrição"));
    }

    [Fact]
    public async Task GerarRiscos_LimitaPSNaMatriz_ESoAceitaNormaValidada()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);
        ia.Riscos = new()
        {
            new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 9, Severidade = 0, Base = new() { "NR-35", "NR-12", "NR-99" },
                Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo" }, new MedidaGerada { Texto = " " } } },
        };

        var n = await new GerarRiscosAtividadeGeracaoPgrCommandHandler(db, ia).Handle(new GerarRiscosAtividadeGeracaoPgrCommand(id, 1, "1.1"), default);

        Assert.Equal(1, n);
        Assert.DoesNotContain(ia.ContextoRiscos!.RequisitosLegais, r => r.StartsWith("NR-12")); // em revisão não vai para a IA
        var r = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes[0].Atividades[0].Riscos.Single();
        Assert.Equal(5, r.Probabilidade);
        Assert.Equal(1, r.Severidade);
        Assert.Equal(new[] { "NR-35" }, r.Base);
        Assert.Single(r.Medidas);
        Assert.False(r.Revisado);
    }

    [Fact]
    public async Task Aprovar_ComPendencia_Recusa_ESemPendencia_CriaPgrVigenteComEstruturaEMedidasValidadas()
    {
        var (db, obra, armador) = await CriarAsync();
        var antigo = new Pgr { ObraId = obra.Id, Nome = "PGR antigo", DataElaboracao = DateTime.UtcNow.AddYears(-1), Status = StatusPgr.Vigente };
        db.Pgrs.Add(antigo);
        await db.SaveChangesAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        var estrutura = new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1, Nome = "Estrutura", Ambiente = "Laje",
                    Funcoes = new() { new FuncaoGerada { FuncaoId = armador.Id, Nome = "ARMADOR", Descricao = "Monta armaduras" } },
                    Atividades = new()
                    {
                        new AtividadeGerada
                        {
                            CodigoEap = "1.1", Nome = "Concretagem de laje", RiscosGerados = true,
                            Riscos = new()
                            {
                                new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 4, Severidade = 5, Revisado = true,
                                    Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Decisao = "Aceita" }, new MedidaGerada { Texto = "Descartar", Decisao = "Descartada" } } },
                                new RiscoGerado { Perigo = "Excluído", Probabilidade = 1, Severidade = 1, Excluido = true },
                            },
                        },
                    },
                },
            },
            PlanoAcao = new() { "Instalar linha de vida" },
        };

        // Com medida pendente, nem envia nem aprova sem resolver.
        estrutura.Ghes[0].Atividades[0].Riscos[0].Medidas[0].Decisao = "Pendente";
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default));

        // Engenheiro devolve; técnico resolve e reenvia; engenheiro aprova.
        await new DevolverGeracaoPgrCommandHandler(db).Handle(new DevolverGeracaoPgrCommand(id, "Decidir medidas"), default);
        estrutura.Ghes[0].Atividades[0].Riscos[0].Medidas[0].Decisao = "Aceita";
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        var pgrId = await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default);

        var pgr = await db.Pgrs.SingleAsync(p => p.Id == pgrId);
        Assert.Equal(StatusPgr.Vigente, pgr.Status);
        Assert.Equal(StatusPgr.Encerrado, (await db.Pgrs.SingleAsync(p => p.Id == antigo.Id)).Status);
        var ghe = await db.Ghes.Include(g => g.Funcoes).SingleAsync();
        Assert.Equal("Monta armaduras", ghe.Funcoes.Single().DescricaoAtividades);
        Assert.Equal(armador.Id, ghe.Funcoes.Single().FuncaoId);
        var atividade = await db.Atividades.SingleAsync();
        Assert.Equal("1.1 Concretagem de laje", atividade.Nome);
        var risco = await db.Riscos.SingleAsync();
        Assert.Equal(NivelRisco.Critico, risco.NivelRisco);
        Assert.Equal("Eng. Carlos", risco.AvaliadoPorNome);
        var medida = await db.MedidasPrevencao.SingleAsync();
        Assert.Equal(StatusMedidaPrevencao.Validada, medida.Status);
        Assert.Equal(FaseMedidaPrevencao.Antes, medida.Fase);
        Assert.Single(await db.PlanoAcaoItens.Where(i => i.PgrId == pgrId).ToListAsync());
        var g = await db.GeracoesPgrEap.SingleAsync();
        Assert.Equal(StatusGeracaoPgrEap.Aprovado, g.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default));
    }

    private static async Task<Guid> CriarGeracaoComDuasAtividadesAsync(Infrastructure.Persistencia.SstDbContext db, Obra obra, IaFalsa ia)
    {
        ia.Itens = new()
        {
            new ItemEapGerado { Codigo = "1.1", Nome = "Concretagem de laje", Nivel = 2 },
            new ItemEapGerado { Codigo = "1.2", Nome = "Armação de pilares", Nivel = 2 },
        };
        return await new CriarGeracaoPgrEapCommandHandler(db, new ExtratorFalso(), ia).Handle(new CriarGeracaoPgrEapCommand(
            obra.Id, "PGR Obra Teste", new DateTime(2026, 10, 21), null, "eap.xlsx", "application/x", new byte[] { 1 }, null, "Téc. Ana"), default);
    }

    [Fact]
    public async Task SugerirGhes_AtividadeRepetidaEmDoisGhes_FicaSoNoPrimeiro_EGheComMesmoNomeViraUm()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoComDuasAtividadesAsync(db, obra, ia);
        ia.Ghes = new()
        {
            new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "Armador", Descricao = "a" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } },
            new GheGerado { Nome = "estrutura ", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "b" }, new FuncaoGerada { Nome = "Pedreiro", Descricao = "c" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" }, new AtividadeGerada { CodigoEap = "1.2" } } },
        };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        var ghe = Assert.Single(dto.Estrutura.Ghes);
        Assert.Equal(new[] { "1.1", "1.2" }, ghe.Atividades.Select(a => a.CodigoEap).ToArray());
        Assert.Equal(2, ghe.Funcoes.Count); // Armador não repete
    }

    [Fact]
    public async Task Pendencias_AtividadeDaEapSemGhe_EmDoisGhes_ENomeRepetido_BloqueiamAprovacao()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoComDuasAtividadesAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "Armador", Descricao = "a" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        Assert.Contains(dto.PendenciasAprovacao, p => p.Contains("sem GHE: 1.2"));

        // Edição manual que reintroduz as duplicatas.
        var e = dto.Estrutura;
        var copia = new GheGerado { Numero = 1, Nome = "ESTRUTURA", Funcoes = e.Ghes[0].Funcoes, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1", Nome = "Concretagem de laje" } } };
        e.Ghes.Add(copia);
        var p = PendenciasGeracaoPgr.Calcular(e, 5, 5);
        Assert.Contains(p, x => x.Contains("Número de GHE repetido"));
        Assert.Contains(p, x => x.Contains("nome repetido"));
        Assert.Contains(p, x => x.Contains("mais de um GHE"));
    }

    [Fact]
    public async Task GerarRiscos_RefazerNaoDuplicaPerigoJaRevisado_NemPerigoRepetidoNaResposta()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);
        ia.Riscos = new() { new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 2, Severidade = 2 } };
        await new GerarRiscosAtividadeGeracaoPgrCommandHandler(db, ia).Handle(new GerarRiscosAtividadeGeracaoPgrCommand(id, 1, "1.1"), default);

        // Engenheiro revisa o risco gerado e manda refazer: a IA repete o perigo (com outra caixa) e ainda duplica um novo.
        var estrutura = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura;
        estrutura.Ghes[0].Atividades[0].Riscos[0].Revisado = true;
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        ia.Riscos = new()
        {
            new RiscoGerado { Tipo = "Acidente", Perigo = "QUEDA DE ALTURA", Probabilidade = 3, Severidade = 3 },
            new RiscoGerado { Tipo = "Físico", Perigo = "Ruído do vibrador", Probabilidade = 2, Severidade = 2 },
            new RiscoGerado { Tipo = "Físico", Perigo = "ruido do vibrador", Probabilidade = 2, Severidade = 2 },
        };

        var n = await new GerarRiscosAtividadeGeracaoPgrCommandHandler(db, ia).Handle(new GerarRiscosAtividadeGeracaoPgrCommand(id, 1, "1.1"), default);

        var riscos = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes[0].Atividades[0].Riscos;
        Assert.Equal(new[] { "Queda de altura", "Ruído do vibrador" }, riscos.Select(r => r.Perigo).ToArray());
        Assert.Equal(1, n);
    }
}
