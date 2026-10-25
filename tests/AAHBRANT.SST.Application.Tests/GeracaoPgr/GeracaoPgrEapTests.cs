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

        public Task<SugestaoEmergenciaIa> SugerirEmergenciaAsync(ContextoEmergenciaIa contexto, CancellationToken ct) =>
            Task.FromResult(new SugestaoEmergenciaIa(new List<RecomendacaoEmergenciaIa>(), new List<string>()));
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

    private static DadosComplementaresPgr ComplementaresCompletos()
    {
        var c = new DadosComplementaresPgr
        {
            Cnae = "4211-1/01 — Construção de rodovias", GrauRisco = 3,
            ResponsavelTecnicoRegistro = "CREA 12345", ResponsavelImplantacaoNome = "Enc. João",
        };
        c.SemearPadroes();
        c.ContatosEmergencia.Single(x => x.Chave == "hospital").Telefone = "(31) 3333-4444";
        return c;
    }

    [Fact]
    public void Complementares_NascemComContatosENumerosNacionais_ENaoSaoReSemeadosDepoisDeEditados()
    {
        var e = new EstruturaGeracaoPgr().Normalizar();

        Assert.Equal("192", e.Complementares.ContatosEmergencia.Single(x => x.Chave == "samu").Telefone);
        Assert.Equal("193", e.Complementares.ContatosEmergencia.Single(x => x.Chave == "bombeiros").Telefone);
        Assert.Equal("190", e.Complementares.ContatosEmergencia.Single(x => x.Chave == "pm").Telefone);
        Assert.Null(e.Complementares.ContatosEmergencia.Single(x => x.Chave == "hospital").Telefone); // o técnico informa
        Assert.Equal(5, e.Complementares.CenariosEmergencia.Count);

        e.Complementares.ContatosEmergencia.Single(x => x.Chave == "hospital").Telefone = "(31) 3333-4444";
        e.Complementares.CenariosEmergencia.RemoveAt(0);
        e.Normalizar();
        Assert.Equal("(31) 3333-4444", e.Complementares.ContatosEmergencia.Single(x => x.Chave == "hospital").Telefone);
        Assert.Equal(4, e.Complementares.CenariosEmergencia.Count);
    }

    [Fact]
    public async Task NovaGeracao_HerdaOsDadosComplementaresDaUltimaAprovadaDaObra()
    {
        var (db, obra, _) = await CriarAsync();
        var anterior = new EstruturaGeracaoPgr { Complementares = ComplementaresCompletos() };
        db.GeracoesPgrEap.Add(new GeracaoPgrEap
        {
            ObraId = obra.Id, Nome = "PGR anterior", DataElaboracao = DateTime.UtcNow.AddYears(-1), NomeArquivoEap = "a.xlsx",
            Status = StatusGeracaoPgrEap.Aprovado, AprovadoEmUtc = DateTime.UtcNow.AddMonths(-3),
            EstruturaJson = System.Text.Json.JsonSerializer.Serialize(anterior, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
        });
        await db.SaveChangesAsync();

        var id = await CriarGeracaoAsync(db, obra, new IaFalsa());

        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        Assert.Equal("CREA 12345", dto.Estrutura.Complementares.ResponsavelTecnicoRegistro);
        Assert.Equal("(31) 3333-4444", dto.Estrutura.Complementares.ContatosEmergencia.Single(x => x.Chave == "hospital").Telefone);
        Assert.Null(dto.Estrutura.DataTermino); // a vigência é nova a cada geração
    }

    // Estrutura sem nenhuma pendência, com um EPI do catálogo ligado à medida.
    private static EstruturaGeracaoPgr EstruturaValida(Guid? epi, Action<RiscoGerado>? ajustarRisco = null)
    {
        var risco = new RiscoGerado
        {
            Tipo = "Físico", Perigo = "Ruído de equipamentos", Probabilidade = 2, Severidade = 2, Revisado = true,
            Medidas = new() { new MedidaGerada { Texto = "Protetor auricular", Categoria = CategoriasMedida.Epi, CatalogoEpiId = epi, Decisao = "Aceita" } },
        };
        ajustarRisco?.Invoke(risco);
        return new EstruturaGeracaoPgr
        {
            DataTermino = new DateTime(2028, 1, 1),
            Complementares = ComplementaresCompletos(),
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1, Nome = "Estrutura",
                    Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "Monta armaduras" } },
                    Atividades = new() { new AtividadeGerada { CodigoEap = "1.1", Nome = "Laje", RiscosGerados = true, Riscos = new() { risco } } },
                },
            },
        }.Normalizar();
    }

    [Fact]
    public void Pendencias_QuantitativoExigeMedicaoCompleta()
    {
        Assert.Empty(PendenciasGeracaoPgr.Calcular(EstruturaValida(null), 5, 5));

        var semMedicao = EstruturaValida(null, r => r.Exposicao.Criterio = CriteriosExposicao.Quantitativo);
        Assert.Contains(PendenciasGeracaoPgr.Calcular(semMedicao, 5, 5), p => p.Contains("quantitativo") && p.Contains("Ruído"));

        var incompleta = EstruturaValida(null, r =>
        {
            r.Exposicao.Criterio = CriteriosExposicao.Quantitativo;
            r.Exposicao.Medicoes.Add(new MedicaoExposicao { Data = new DateTime(2026, 10, 1), Unidade = "dB(A)" }); // sem valor
        });
        Assert.Contains(PendenciasGeracaoPgr.Calcular(incompleta, 5, 5), p => p.Contains("incompleta"));

        var completa = EstruturaValida(null, r =>
        {
            r.Exposicao.Criterio = CriteriosExposicao.Quantitativo;
            r.Exposicao.Medicoes.Add(new MedicaoExposicao { Data = new DateTime(2026, 10, 1), Valor = 82.5m, Unidade = "dB(A)", LimiteTolerancia = "85 dB(A)" });
        });
        Assert.Empty(PendenciasGeracaoPgr.Calcular(completa, 5, 5));
    }

    [Fact]
    public void Pendencias_ConferemOCaDoEpiLigadoNaMedida()
    {
        var ok = Guid.NewGuid();
        var vencido = Guid.NewGuid();
        var semCa = Guid.NewGuid();
        var removido = Guid.NewGuid();
        var epis = new Dictionary<Guid, CatalogoEpiInfo>
        {
            [ok] = new("Protetor auricular", "12345", DateTime.UtcNow.AddYears(1)),
            [vencido] = new("Luva", "999", DateTime.UtcNow.AddDays(-3)),
            [semCa] = new("Bota", null, null),
        };

        Assert.Empty(PendenciasGeracaoPgr.Calcular(EstruturaValida(ok), 5, 5, epis));
        Assert.Contains(PendenciasGeracaoPgr.Calcular(EstruturaValida(vencido), 5, 5, epis), p => p.Contains("venceu em"));
        Assert.Contains(PendenciasGeracaoPgr.Calcular(EstruturaValida(semCa), 5, 5, epis), p => p.Contains("sem número de CA"));
        Assert.Contains(PendenciasGeracaoPgr.Calcular(EstruturaValida(removido), 5, 5, epis), p => p.Contains("não existe mais"));
        // EPI sem vínculo com o catálogo não bloqueia: o PDF mostra só o texto da medida.
        Assert.Empty(PendenciasGeracaoPgr.Calcular(EstruturaValida(null), 5, 5, epis));
    }

    [Fact]
    public void Cronograma_MarcaOsMesesDeInicioAPrazo_EAnoACano()
    {
        var inicio = new DateTime(2026, 11, 10);
        var prazo = new DateTime(2027, 1, 20);

        var em2026 = CronogramaPlano.MesesAtivos(inicio, prazo, 2026);
        var em2027 = CronogramaPlano.MesesAtivos(inicio, prazo, 2027);
        Assert.Equal(new[] { 11, 12 }, Enumerable.Range(1, 12).Where(m => em2026[m - 1]).ToArray());
        Assert.Equal(new[] { 1 }, Enumerable.Range(1, 12).Where(m => em2027[m - 1]).ToArray());

        // Sem início: só o mês do prazo. Início depois do prazo: também só o prazo. Sem prazo: nada.
        Assert.Equal(new[] { 12 }, Enumerable.Range(1, 12).Where(m => CronogramaPlano.MesesAtivos(null, new DateTime(2026, 12, 15), 2026)[m - 1]).ToArray());
        Assert.Equal(new[] { 3 }, Enumerable.Range(1, 12).Where(m => CronogramaPlano.MesesAtivos(new DateTime(2026, 8, 1), new DateTime(2026, 3, 5), 2026)[m - 1]).ToArray());
        Assert.DoesNotContain(true, CronogramaPlano.MesesAtivos(inicio, null, 2026));
    }

    [Fact]
    public void Pendencias_ExigemTermino_ResponsavelEPrazoDaAcao_ECategoriaDaMedidaAceita()
    {
        var estrutura = new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1, Nome = "Estrutura",
                    Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "Monta armaduras" } },
                    Atividades = new() { new AtividadeGerada { CodigoEap = "1.1", Nome = "Laje", RiscosGerados = true, Riscos = new() {
                        new RiscoGerado { Perigo = "Queda", Probabilidade = 2, Severidade = 2, Revisado = true,
                            Medidas = new() { new MedidaGerada { Texto = "Guarda-corpo", Decisao = "Aceita" } } } } } },
                },
            },
            // Formato antigo: uma ação por linha, sem responsável nem prazo.
            PlanoAcao = new() { "Instalar linha de vida" },
        }.Normalizar();

        var pendencias = PendenciasGeracaoPgr.Calcular(estrutura, 5, 5);

        Assert.Contains(pendencias, p => p.Contains("término da vigência"));
        Assert.Contains(pendencias, p => p.Contains("sem responsável"));
        Assert.Contains(pendencias, p => p.Contains("sem prazo"));
        Assert.Contains(pendencias, p => p.Contains("sem classificação"));
        Assert.Contains(pendencias, p => p.Contains("CNAE"));
        Assert.Contains(pendencias, p => p.Contains("grau de risco"));
        Assert.Contains(pendencias, p => p.Contains("CREA"));
        Assert.Contains(pendencias, p => p.Contains("implantação"));
        Assert.Contains(pendencias, p => p.Contains("hospital"));

        estrutura.Complementares = ComplementaresCompletos();
        estrutura.DataTermino = new DateTime(2027, 1, 1);
        estrutura.AcoesPlano[0].ResponsavelNome = "Téc. Ana";
        estrutura.AcoesPlano[0].Prazo = new DateTime(2026, 12, 1);
        estrutura.Ghes[0].Atividades[0].Riscos[0].Medidas[0].Categoria = CategoriasMedida.Epi;
        Assert.Empty(PendenciasGeracaoPgr.Calcular(estrutura, 5, 5));
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
                                    Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Categoria = CategoriasMedida.Epc, Decisao = "Aceita" }, new MedidaGerada { Texto = "Descartar", Decisao = "Descartada" } } },
                                new RiscoGerado { Perigo = "Excluído", Probabilidade = 1, Severidade = 1, Excluido = true },
                            },
                        },
                    },
                },
            },
            DataTermino = new DateTime(2027, 10, 21),
            AcoesPlano = new() { new AcaoPlanoGerada { Descricao = "Instalar linha de vida", ResponsavelNome = "Téc. Ana", Prazo = new DateTime(2026, 12, 15) } },
            Complementares = ComplementaresCompletos(),
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
        Assert.Equal(new DateTime(2027, 10, 21), pgr.DataTermino);
        var acao = Assert.Single(await db.PlanoAcaoItens.Where(i => i.PgrId == pgrId).ToListAsync());
        Assert.Equal(new DateTime(2026, 12, 15), acao.Prazo);
        var g = await db.GeracoesPgrEap.SingleAsync();
        Assert.Equal(StatusGeracaoPgrEap.Aprovado, g.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default));
    }
}
