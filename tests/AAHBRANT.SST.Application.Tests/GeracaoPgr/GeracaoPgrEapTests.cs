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
        public List<string> AtividadesEnviadas { get; private set; } = new();
        public Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct)
        {
            AtividadesEnviadas = atividades.Select(a => a.Codigo).ToList();
            return Task.FromResult(Ghes);
        }
        public Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade contexto, CancellationToken ct)
        {
            ContextoRiscos = contexto;
            return Task.FromResult(Riscos);
        }

        public List<AcompanhamentoSugeridoIa> Acompanhamentos { get; set; } = new();

        public ContextoAcompanhamentoPlano? ContextoAcompanhamento { get; private set; }

        public Task<List<AcompanhamentoSugeridoIa>> SugerirAcompanhamentoAsync(ContextoAcompanhamentoPlano contexto, CancellationToken ct) { ContextoAcompanhamento = contexto; return Task.FromResult(Acompanhamentos); }

        public List<CboSugeridoIa> Cbos { get; set; } = new();
        public List<FuncaoParaCbo> CboPedidos { get; private set; } = new();
        public Task<List<CboSugeridoIa>> SugerirCboAsync(IReadOnlyList<FuncaoParaCbo> funcoes, CancellationToken ct)
        {
            CboPedidos = funcoes.ToList();
            return Task.FromResult(Cbos);
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
    public async Task Criar_SempreIncluiAdministrativoNoInicio_MarcadoParaUsar()
    {
        var (db, obra, _) = await CriarAsync();
        var id = await CriarGeracaoAsync(db, obra, new IaFalsa());

        var g = await db.GeracoesPgrEap.SingleAsync(x => x.Id == id);
        var itens = SerializacaoGeracaoTeste.Ler(g.EstruturaJson).Itens;
        Assert.Equal(new[] { "ADM", "ADM.1", "1", "1.1", "9" }, itens.Select(i => i.Codigo));
        Assert.True(itens[0].Usar && itens[1].Usar);
        Assert.Equal(2, itens[1].Nivel);

        // Não duplica se a estrutura já tem o administrativo.
        var e = new EstruturaGeracaoPgr { Itens = itens };
        ItemAdministrativoEap.Garantir(e);
        Assert.Single(e.Itens, i => i.Codigo == "ADM.1");
    }

    [Fact]
    public void Medida_SoDescartadaFicaForaDoPgr()
    {
        Assert.True(new MedidaGerada { Decisao = "Pendente" }.EntraNoPgr());
        Assert.True(new MedidaGerada { Decisao = "Aceita" }.EntraNoPgr());
        Assert.False(new MedidaGerada { Decisao = "Descartada" }.EntraNoPgr());
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
        // O ADM.1 padrão, que a IA falsa não agrupou, vai para o GHE Administrativo (no fim).
        var ghe = Assert.Single(dto.Estrutura.Ghes, g => g.Nome != ItemAdministrativoEap.NomeGhe);
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
            // Assinatura à mão: estes testes não exercitam o motor de assinatura eletrônica.
            Tecnico = new AssinanteGeracaoPgr { AMao = true }, Engenheiro = new AssinanteGeracaoPgr { AMao = true },
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

        // Medida sem decisão vale como aceita (regra do padrão, 29/10/2026); o que ainda trava é o risco não revisado.
        estrutura.Ghes[0].Atividades[0].Riscos[0].Medidas[0].Decisao = "Pendente";
        estrutura.Ghes[0].Atividades[0].Riscos[0].Revisado = false;
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default));

        // Engenheiro devolve; técnico resolve e reenvia; engenheiro aprova.
        await new DevolverGeracaoPgrCommandHandler(db).Handle(new DevolverGeracaoPgrCommand(id, "Revisar o risco"), default);
        estrutura.Ghes[0].Atividades[0].Riscos[0].Revisado = true;
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        var pgrId = await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default);

        // Obra que já tinha PGR vigente: a geração vira revisão dele, não um PGR separado.
        Assert.Equal(antigo.Id, pgrId);
        var pgr = await db.Pgrs.SingleAsync();
        Assert.Equal(StatusPgr.Vigente, pgr.Status);
        var revisao = Assert.Single(await db.PgrRevisoes.Where(r => r.PgrId == pgr.Id).ToListAsync());
        Assert.Equal(1, revisao.NumeroRevisao);
        Assert.Contains("Eng. Carlos", revisao.Motivo);
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
        var ghe = Assert.Single(dto.Estrutura.Ghes, g => g.Nome != ItemAdministrativoEap.NomeGhe);
        Assert.Equal(new[] { "1.1", "1.2" }, ghe.Atividades.Select(a => a.CodigoEap).ToArray());
        Assert.Equal(2, ghe.Funcoes.Count); // Armador não repete
    }

    private static async Task IncluirItensManuaisAsync(Infrastructure.Persistencia.SstDbContext db, Guid id, params ItemEapGerado[] itens)
    {
        var estrutura = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura;
        estrutura.Itens.AddRange(itens);
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 2, estrutura), default);
    }

    [Fact]
    public async Task SugerirGhes_ItemManualComoPacoteENomeAdministrativo_VaiParaIa_EFicaNoGheAdministrativo()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        await IncluirItensManuaisAsync(db, id, new ItemEapGerado { Codigo = "01.02", Nome = "Administrativo", Nivel = 1, Manual = true });
        // A IA junta o administrativo à frente de obra e esquece o ADM.1 padrão.
        ia.Ghes = new()
        {
            new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } },
                Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" }, new AtividadeGerada { CodigoEap = "01.02" } } },
        };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        Assert.Contains("01.02", ia.AtividadesEnviadas);
        var ghes = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes;
        Assert.Equal(new[] { "1.1" }, ghes.Single(g => g.Nome == "Estrutura").Atividades.Select(a => a.CodigoEap).ToArray());
        var adm = ghes.Single(g => g.Nome == ItemAdministrativoEap.NomeGhe);
        Assert.Equal(new[] { "ADM.1", "01.02" }, adm.Atividades.Select(a => a.CodigoEap).OrderBy(c => c == "01.02").ToArray());
        Assert.Equal("Administrativo", adm.Atividades.Single(a => a.CodigoEap == "01.02").Nome);
    }

    [Fact]
    public async Task SugerirGhes_ItemQueAIaNaoAgrupou_NaoSome_VaiParaGheAConferir()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        await IncluirItensManuaisAsync(db, id, new ItemEapGerado { Codigo = "M1", Nome = "Ponte provisória", Nivel = 2, Manual = true });
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        var conferir = dto.Estrutura.Ghes.Single(g => g.Atividades.Any(a => a.CodigoEap == "M1"));
        Assert.StartsWith("Itens a conferir", conferir.Nome);
        Assert.Empty(conferir.Funcoes);
        Assert.Contains(dto.PendenciasAprovacao, p => p.Contains($"GHE {conferir.Numero:00} sem funções"));
        Assert.DoesNotContain(dto.PendenciasAprovacao, p => p.Contains("sem GHE"));
    }

    [Theory]
    [InlineData("Ajudante/Servente", true)]
    [InlineData("Servente", true)]
    [InlineData("Ajudante de obras", true)]
    [InlineData("AJUDANTE GERAL", true)]
    [InlineData("Auxiliar de serviços gerais", true)]
    [InlineData("Ajudante de Eletricista", false)]
    [InlineData("Auxiliar de Topógrafo", false)]
    [InlineData("Auxiliar administrativo", false)]
    [InlineData("Pedreiro", false)]
    public void AjudanteGeral_SoOGenericoViraAjudanteGeral(string nome, bool generico) =>
        Assert.Equal(generico, AjudanteGeral.EhGenerico(nome));

    [Fact]
    public async Task SugerirGhes_AjudanteGenericoViraAjudanteGeralEmTodoGhe_ComADescricaoDeCada()
    {
        var (db, obra, _) = await CriarAsync();
        var ajudanteCadastro = new Funcao { Nome = "AJUDANTE GERAL" };
        db.Funcoes.Add(ajudanteCadastro);
        await db.SaveChangesAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoComDuasAtividadesAsync(db, obra, ia);
        ia.Ghes = new()
        {
            new GheGerado
            {
                Nome = "Demolição", Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } },
                Funcoes = new()
                {
                    new FuncaoGerada { Nome = "Ajudante/Servente", Descricao = "Recolhe entulho" },
                    new FuncaoGerada { Nome = "Servente", Descricao = "outra" },
                    new FuncaoGerada { Nome = "Ajudante de Eletricista", Descricao = "Apoia o eletricista" },
                },
            },
            new GheGerado
            {
                Nome = "Pavimentação", Atividades = new() { new AtividadeGerada { CodigoEap = "1.2" } },
                Funcoes = new() { new FuncaoGerada { Nome = "Ajudante de obras", Descricao = "Espalha material" } },
            },
        };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        var ghes = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes;
        var dem = ghes.Single(g => g.Nome == "Demolição").Funcoes;
        Assert.Equal(new[] { "Ajudante Geral", "Ajudante de Eletricista" }, dem.Select(f => f.Nome).ToArray());
        Assert.Equal(("Recolhe entulho", ajudanteCadastro.Id), (dem[0].Descricao, dem[0].FuncaoId));
        var pav = ghes.Single(g => g.Nome == "Pavimentação").Funcoes.Single();
        Assert.Equal(("Ajudante Geral", "Espalha material", ajudanteCadastro.Id), (pav.Nome, pav.Descricao, pav.FuncaoId));
    }

    [Fact]
    public void AjudanteGeral_UmSoCboEmTodosOsGhes_ValeOMaisUsado()
    {
        var e = new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado { Numero = 1, Funcoes = new() { new FuncaoGerada { Nome = "Servente", Descricao = "Auxilia na demolição", Cbo = "715210" } } },
                new GheGerado { Numero = 2, Funcoes = new() { new FuncaoGerada { Nome = "Ajudante Geral", Descricao = "Auxilia na limpeza", Cbo = "717020" } } },
                new GheGerado { Numero = 3, Funcoes = new() { new FuncaoGerada { Nome = "Ajudante de obras", Descricao = "Auxilia no transporte", Cbo = "717020" } } },
                new GheGerado { Numero = 4, Funcoes = new() { new FuncaoGerada { Nome = "Ajudante de Eletricista", Cbo = "715615" } } },
            },
        };

        AjudanteGeral.Unificar(e, null);

        Assert.All(e.Ghes.Take(3).Select(g => g.Funcoes.Single()), f => Assert.Equal(("Ajudante Geral", "717020"), (f.Nome, f.Cbo)));
        Assert.Equal("Auxilia na demolição", e.Ghes[0].Funcoes[0].Descricao); // a descrição continua a de cada GHE
        Assert.Equal("715615", e.Ghes[3].Funcoes[0].Cbo);
    }

    [Fact]
    public void NomesFuncoes_CaixaAlta_EMesmoNomeEmTodosOsGhes()
    {
        var e = new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado { Numero = 1, Nome = "Terraplenagem", Funcoes = new()
                {
                    new FuncaoGerada { Nome = "Greidista / Nivelador" }, new FuncaoGerada { Nome = "OPERADOR DE MÁQUINAS" },
                } },
                new GheGerado { Numero = 2, Nome = "Pavimentação", Funcoes = new() { new FuncaoGerada { Nome = "Greidista" } } },
                new GheGerado { Numero = 3, Nome = "Topografia", Funcoes = new() { new FuncaoGerada { Nome = "Greidista" } } },
            },
        };

        NomesFuncoes.Padronizar(e);

        Assert.Equal(new[] { "Greidista", "Operador de Máquinas" }, e.Ghes[0].Funcoes.Select(f => f.Nome).ToArray());
        Assert.Equal("Greidista", e.Ghes[1].Funcoes.Single().Nome);
    }

    [Theory]
    [InlineData("Encarregado", true)]
    [InlineData("Encarregado de Obras", true)]
    [InlineData("Encarregado de Pavimentação / Pleno", true)]
    [InlineData("ENCARREGADO DE TERRAPLENAGEM", true)]
    [InlineData("Encarregado de Almoxarifado", false)]
    [InlineData("Encarregado Administrativo", false)]
    [InlineData("Engenheiro Civil", false)]
    public void EncarregadoDeObras_SoOEncarregadoDeProducao(string nome, bool encarregado) =>
        Assert.Equal(encarregado, EncarregadoDeObras.EhEncarregado(nome));

    [Fact]
    public void EncarregadoDeObras_UmSoNomeECboEmTodosOsGhes_ComADescricaoDeCada()
    {
        var e = new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado { Numero = 1, Nome = "Demolição", Funcoes = new() { new FuncaoGerada { Nome = "Encarregado", Descricao = "Coordena a demolição", Cbo = "710205" } } },
                new GheGerado { Numero = 2, Nome = "Pavimentação", Funcoes = new() { new FuncaoGerada { Nome = "Encarregado de Pavimentação / Pleno", Descricao = "Coordena a pavimentação", Cbo = "710205" } } },
                new GheGerado { Numero = 3, Nome = "Gestão", Funcoes = new()
                {
                    new FuncaoGerada { Nome = "Encarregado de Obras", Descricao = "Coordena as frentes", Cbo = "710215" },
                    new FuncaoGerada { Nome = "Encarregado de Almoxarifado", Cbo = "410205" },
                } },
            },
        };
        var id = Guid.NewGuid();

        EncarregadoDeObras.Unificar(e, id);

        var enc = e.Ghes.SelectMany(g => g.Funcoes).Where(f => f.Nome == "Encarregado de Obras").ToList();
        Assert.Equal(3, enc.Count);
        Assert.All(enc, f => Assert.Equal(("710205", id), (f.Cbo, f.FuncaoId)));
        Assert.Equal(new[] { "Coordena a demolição", "Coordena a pavimentação", "Coordena as frentes" }, enc.Select(f => f.Descricao).ToArray());
        Assert.Contains(e.Ghes[2].Funcoes, f => f.Nome == "Encarregado de Almoxarifado");
    }

    [Theory]
    [InlineData("Auxilia na demolição e na limpeza da área.", null)]
    [InlineData("Faz limpeza das áreas do canteiro.", "não começa")]
    [InlineData("Auxilia no transporte e dirige o caminhão quando preciso.", "dirige")]
    [InlineData("Auxilia na terraplenagem e opera o rolo compactador.", "opera")]
    public void AjudanteGeral_DescricaoSoAuxilia(string descricao, string? problema)
    {
        var r = AjudanteGeral.ProblemaDescricao(descricao);
        if (problema is null) Assert.Null(r);
        else Assert.Contains(problema, r);
    }

    [Fact]
    public async Task SugerirGhes_IaTrocaCboDoCadastroQueNaoCombinaComAAtividade_EAjudanteForaDaRegraVaiParaPendencias()
    {
        var (db, obra, _) = await CriarAsync();
        db.TitulosCbo.AddRange(
            new TituloCbo { Codigo = "717020", Titulo = "Servente de obras", Oficial = true },
            new TituloCbo { Codigo = "414205", Titulo = "Apontador de mão de obra", Oficial = true },
            new TituloCbo { Codigo = "414205", Titulo = "Apontador de obras", Oficial = false });
        var apontador = new Funcao { Nome = "Apontador", CboCodigo = "7170-20" };
        db.Funcoes.Add(apontador);
        await db.SaveChangesAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } }, Funcoes = new()
        {
            new FuncaoGerada { Nome = "Apontador", Descricao = "Registra presença e horas" },
            new FuncaoGerada { Nome = "Servente", Descricao = "Faz a limpeza da frente" },
        } } };
        ia.Cbos = new() { new("Apontador", "414205"), new("Ajudante Geral", "717020") };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        Assert.Equal("717020", ia.CboPedidos.Single(p => p.Nome == "Apontador").CboAtual);
        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        var funcoes = dto.Estrutura.Ghes.Single(g => g.Nome == "Estrutura").Funcoes;
        Assert.Equal(("414205", true), (funcoes.Single(f => f.Nome == "Apontador").Cbo, funcoes.Single(f => f.Nome == "Apontador").CboSugeridoPelaIa));
        Assert.Equal(("717020", true), (funcoes.Single(f => f.Nome == "Ajudante Geral").Cbo, funcoes.Single(f => f.Nome == "Ajudante Geral").CboSugeridoPelaIa));
        Assert.Contains(dto.PendenciasAprovacao, p => p.Contains("Ajudante Geral com descrição que não começa"));
    }

    [Fact]
    public async Task SugerirGhes_AjudanteGeral_RecebeServenteDeObrasComoCandidato()
    {
        var (db, obra, _) = await CriarAsync();
        await CarregarTabelaCboAsync(db);
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } },
            Funcoes = new() { new FuncaoGerada { Nome = "Servente", Descricao = "Auxilia" } } } };
        ia.Cbos = new() { new("Ajudante Geral", "717020") };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        Assert.Equal("717020", ia.CboPedidos.Single().Candidatos[0].Codigo);
        var f = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes[0].Funcoes.Single();
        Assert.Equal(("Ajudante Geral", "717020"), (f.Nome, f.Cbo));
    }

    [Fact]
    public async Task Aprovacao_RascunhoAntigoComAjudanteServente_CriaUmaFuncaoAjudanteGeral()
    {
        var (db, obra, _) = await CriarAsync();
        var estrutura = EstruturaSimplesAprovavel("Instalar linha de vida");
        estrutura.Ghes[0].Funcoes.Add(new FuncaoGerada { Nome = "Ajudante/Servente", Descricao = "Auxilia a armação" });

        await AprovarAsync(db, obra, new IaFalsa(), estrutura);

        Assert.True(await db.Funcoes.AnyAsync(f => f.Nome == "Ajudante Geral"));
        Assert.False(await db.Funcoes.AnyAsync(f => f.Nome == "Ajudante/Servente"));
        var ghe = await db.Ghes.Include(g => g.Funcoes).ThenInclude(f => f.Funcao).SingleAsync(g => g.Ativo);
        Assert.Equal("Auxilia a armação", ghe.Funcoes.Single(f => f.Funcao!.Nome == "Ajudante Geral").DescricaoAtividades);
    }

    [Fact]
    public void Administrativo_ReconheceCodigoENome_MasNaoCanteiroDeObra()
    {
        Assert.True(ItemAdministrativoEap.EhAdministrativo(new ItemEapGerado { Codigo = "ADM.1", Nome = "x" }));
        Assert.True(ItemAdministrativoEap.EhAdministrativo(new ItemEapGerado { Codigo = "01.02", Nome = "Administrativo" }));
        Assert.True(ItemAdministrativoEap.EhAdministrativo(new ItemEapGerado { Codigo = "9", Nome = "Escritório da obra" }));
        Assert.False(ItemAdministrativoEap.EhAdministrativo(new ItemEapGerado { Codigo = "1.1", Nome = "Mobilização do canteiro" }));
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
        // A IA não agrupou o 1.2: ele não some, fica num GHE a conferir. Se alguém o tirar de lá à mão, trava.
        var e = dto.Estrutura;
        Assert.StartsWith("Itens a conferir", e.Ghes.Single(g => g.Atividades.Any(a => a.CodigoEap == "1.2")).Nome);
        e.Ghes.RemoveAll(g => g.Nome.StartsWith("Itens a conferir"));
        Assert.Contains(PendenciasGeracaoPgr.Calcular(e, 5, 5), p => p.Contains("sem GHE: 1.2"));

        // Edição manual que reintroduz as duplicatas.
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

    [Fact]
    public async Task GerarRiscos_LimitaAoTetoPorAtividade_MantendoOsDeMaiorPxS()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);
        ia.Riscos = new()
        {
            new RiscoGerado { Tipo = "Físico", Perigo = "Ruído", Probabilidade = 1, Severidade = 1 },
            new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 3, Severidade = 5 },
            new RiscoGerado { Tipo = "Acidente", Perigo = "Choque elétrico", Probabilidade = 2, Severidade = 5 },
            new RiscoGerado { Tipo = "Físico", Perigo = "Vibração", Probabilidade = 1, Severidade = 2 },
            new RiscoGerado { Tipo = "Químico", Perigo = "Poeira de cimento, sílica e materiais de obra", Probabilidade = 3, Severidade = 3 },
            new RiscoGerado { Tipo = "Ergonômico", Perigo = "Esforço físico e levantamento de peso", Probabilidade = 2, Severidade = 3 },
            new RiscoGerado { Tipo = "Acidente", Perigo = "Corte e perfuração por ferramentas e materiais", Probabilidade = 3, Severidade = 2 },
        };

        var n = await new GerarRiscosAtividadeGeracaoPgrCommandHandler(db, ia).Handle(new GerarRiscosAtividadeGeracaoPgrCommand(id, 1, "1.1"), default);

        var riscos = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.Ghes[0].Atividades[0].Riscos;
        Assert.Equal(PerigosPadrao.MaxRiscosPorAtividade, n);
        Assert.Equal(new[] { "Queda de altura", "Choque elétrico", "Poeira de cimento, sílica e materiais de obra", "Esforço físico e levantamento de peso", "Corte e perfuração por ferramentas e materiais" }, riscos.Select(r => r.Perigo).ToArray());
    }

    [Fact]
    public void CatalogoDePerigosPadrao_NaoTemNomeRepetido()
    {
        var nomes = PerigosPadrao.Catalogo.Select(p => AAHBRANT.SST.Application.Common.FuncaoSstClassifier.Normalizar(p.Nome)).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct().Count());
    }

    [Fact]
    public async Task Excluir_RascunhoSomem_MasEnviadoParaAprovacaoNaoPodeSerExcluido()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var rascunho = await CriarGeracaoAsync(db, obra, ia);
        var enviado = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new() { new GheGerado { Nome = "Estrutura", Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "x" } }, Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } } } };
        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(enviado), default);
        ia.Riscos = new() { new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 2, Severidade = 2 } };
        await new GerarRiscosAtividadeGeracaoPgrCommandHandler(db, ia).Handle(new GerarRiscosAtividadeGeracaoPgrCommand(enviado, 1, "1.1"), default);
        var geracaoEnviada = await db.GeracoesPgrEap.SingleAsync(g => g.Id == enviado);
        var opcoesJson = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var estruturaEnviada = System.Text.Json.JsonSerializer.Deserialize<EstruturaGeracaoPgr>(geracaoEnviada.EstruturaJson, opcoesJson)!;
        estruturaEnviada.Complementares ??= new DadosComplementaresPgr();
        estruturaEnviada.Complementares.Tecnico = new AssinanteGeracaoPgr { AMao = true };
        estruturaEnviada.Complementares.Engenheiro = new AssinanteGeracaoPgr { AMao = true };
        geracaoEnviada.EstruturaJson = System.Text.Json.JsonSerializer.Serialize(estruturaEnviada, opcoesJson);
        await db.SaveChangesAsync();
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(enviado), default);

        await new ExcluirGeracaoPgrCommandHandler(db).Handle(new ExcluirGeracaoPgrCommand(rascunho), default);

        Assert.False(await db.GeracoesPgrEap.AnyAsync(g => g.Id == rascunho));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExcluirGeracaoPgrCommandHandler(db).Handle(new ExcluirGeracaoPgrCommand(enviado), default));
        Assert.True(await db.GeracoesPgrEap.AnyAsync(g => g.Id == enviado));
    }

    private static EstruturaGeracaoPgr EstruturaSimplesAprovavel(string acao) => new()
    {
        Ghes = new()
        {
            new GheGerado
            {
                Numero = 1, Nome = "Estrutura", Ambiente = "Laje",
                Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "Monta armaduras" } },
                Atividades = new()
                {
                    new AtividadeGerada
                    {
                        CodigoEap = "1.1", Nome = "Concretagem de laje", RiscosGerados = true,
                        Riscos = new()
                        {
                            new RiscoGerado { Tipo = "Acidente", Perigo = "Queda de altura", Probabilidade = 4, Severidade = 5, Revisado = true,
                                Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Categoria = CategoriasMedida.Epc, Decisao = "Aceita" } } },
                        },
                    },
                },
            },
        },
        DataTermino = new DateTime(2027, 10, 21),
        AcoesPlano = new() { new AcaoPlanoGerada { Descricao = acao, ResponsavelNome = "Téc. Ana", Prazo = new DateTime(2026, 12, 15) } },
        Complementares = ComplementaresCompletos(),
    };

    // Recorte da tabela oficial da CBO 2002 para os testes (códigos e títulos reais).
    private static async Task CarregarTabelaCboAsync(Infrastructure.Persistencia.SstDbContext db)
    {
        db.TitulosCbo.AddRange(
            new TituloCbo { Codigo = "715210", Titulo = "Pedreiro", Oficial = true },
            new TituloCbo { Codigo = "717020", Titulo = "Servente de obras", Oficial = true },
            new TituloCbo { Codigo = "715205", Titulo = "Calceteiro", Oficial = true },
            new TituloCbo { Codigo = "715205", Titulo = "Rasteleiro de asfalto", Oficial = false },
            new TituloCbo { Codigo = "715315", Titulo = "Armador de estrutura de concreto armado", Oficial = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SugerirGhes_CboDoCadastroValidoEntra_EIaSoEscolheEntreOsCandidatos()
    {
        var (db, obra, armador) = await CriarAsync();
        armador.CboCodigo = "7153-15";
        await CarregarTabelaCboAsync(db);
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        ia.Ghes = new()
        {
            new GheGerado
            {
                Nome = "Estrutura", Atividades = new() { new AtividadeGerada { CodigoEap = "1.1" } },
                Funcoes = new()
                {
                    new FuncaoGerada { Nome = "Armador", Descricao = "x" },
                    new FuncaoGerada { Nome = "Rasteleiro", Descricao = "Espalha asfalto" },
                    new FuncaoGerada { Nome = "Pedreiro", Descricao = "x" },
                },
            },
        };
        // A IA tenta um código fora dos candidatos do pedreiro: é ignorado.
        ia.Cbos = new() { new("Rasteleiro", "715205"), new("Pedreiro", "999999") };

        await new SugerirGhesGeracaoPgrCommandHandler(db, ia).Handle(new SugerirGhesGeracaoPgrCommand(id), default);

        // O CBO do cadastro também vai para a IA revisar, como "CBO atual"; ela não trocou, então fica.
        Assert.Equal("715315", ia.CboPedidos.Single(p => p.Nome == "Armador").CboAtual);
        Assert.Contains(ia.CboPedidos.Single(p => p.Nome == "Rasteleiro").Candidatos, c => c.Codigo == "715205");
        var dto = await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default);
        var funcoes = dto.Estrutura.Ghes.Single(g => g.Nome == "Estrutura").Funcoes;
        var arm = funcoes.Single(f => f.Nome == "Armador");
        Assert.Equal(("715315", false), (arm.Cbo, arm.CboSugeridoPelaIa));
        Assert.Equal("Armador de estrutura de concreto armado", arm.CboTitulo);
        var rast = funcoes.Single(f => f.Nome == "Rasteleiro");
        Assert.Equal(("715205", true, "Calceteiro"), (rast.Cbo, rast.CboSugeridoPelaIa, rast.CboTitulo));
        Assert.Null(funcoes.Single(f => f.Nome == "Pedreiro").Cbo);
        Assert.Contains(dto.PendenciasAprovacao, p => p.Contains("função Pedreiro sem CBO"));
    }

    [Fact]
    public async Task Envio_FuncaoSemCboDaTabelaOficial_Trava_EAprovacaoGravaCboNoCadastro()
    {
        var (db, obra, _) = await CriarAsync();
        await CarregarTabelaCboAsync(db);
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        var estrutura = EstruturaSimplesAprovavel("Instalar linha de vida");
        estrutura.Ghes[0].Funcoes[0].Cbo = "717010 - Operador de martelete"; // valor antigo, fora do recorte da tabela
        estrutura.Ghes[0].Funcoes.Add(new FuncaoGerada { Nome = "Rasteleiro", Descricao = "Espalha asfalto" });
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default));
        Assert.Contains("não está na tabela oficial", erro.Message);
        Assert.Contains("função Rasteleiro sem CBO", erro.Message);

        estrutura.Ghes[0].Funcoes[0].Cbo = "7153-15";
        estrutura.Ghes[0].Funcoes[1].Cbo = "715205";
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default);

        Assert.Equal("7153-15", (await db.Funcoes.SingleAsync(f => f.Nome == "ARMADOR")).CboCodigo);
        Assert.Equal("7152-05", (await db.Funcoes.SingleAsync(f => f.Nome == "Rasteleiro")).CboCodigo);
    }

    private static async Task<Guid> AprovarAsync(Infrastructure.Persistencia.SstDbContext db, Obra obra, IaFalsa ia, EstruturaGeracaoPgr estrutura)
    {
        var id = await CriarGeracaoAsync(db, obra, ia);
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default);
        return await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default);
    }

    // Registrador falso: grava o signatário como o real faz, sem auditoria nem PDF.
    private sealed class RegistradorFalso : AAHBRANT.SST.Application.Assinatura.IRegistradorAssinaturaService
    {
        private readonly Infrastructure.Persistencia.SstDbContext _db;
        public RegistradorFalso(Infrastructure.Persistencia.SstDbContext db) => _db = db;

        public async Task<AAHBRANT.SST.Application.Assinatura.Queries.DocumentoSignatarioDto> RegistrarAsync(
            Guid documentoAssinaturaId, AAHBRANT.SST.Application.Assinatura.ResultadoAutenticacaoAssinatura resultado, string? ipAddress,
            CancellationToken ct, byte[]? fotoEvidenciaConteudo = null, string? fotoEvidenciaContentType = null, PapelAssinatura? papel = null,
            AAHBRANT.SST.Application.Assinatura.LocalizacaoAssinatura? localizacao = null)
        {
            var agora = DateTime.UtcNow;
            _db.DocumentoSignatarios.Add(new DocumentoSignatario
            {
                DocumentoAssinaturaId = documentoAssinaturaId, TrabalhadorId = resultado.TrabalhadorId,
                MetodoAutenticacao = resultado.Metodo, Papel = papel, AssinadoEm = agora,
            });
            await _db.SaveChangesAsync(ct);
            return new AAHBRANT.SST.Application.Assinatura.Queries.DocumentoSignatarioDto(resultado.TrabalhadorId, "x", resultado.Metodo, agora);
        }
    }

    [Fact]
    public async Task Assinatura_TecnicoAssinaAoEnviar_SoOEscolhido_EDevolverDerrubaAAssinatura_EEngenheiroAssinaAoAprovar()
    {
        var (db, obra, _) = await CriarAsync();
        var funcao = new Funcao { Nome = "TEC" };
        var wTec = new Trabalhador { Nome = "Téc. Ana", Cpf = "11122233344", Funcao = funcao, DataAdmissao = DateTime.UtcNow };
        var wEng = new Trabalhador { Nome = "Eng. Carlos", Cpf = "11122233355", Funcao = funcao, DataAdmissao = DateTime.UtcNow };
        var uTec = new Usuario { Nome = "Téc. Ana", Email = "ana@x.com", TrabalhadorId = null };
        var uEng = new Usuario { Nome = "Eng. Carlos", Email = "carlos@x.com" };
        var uOutro = new Usuario { Nome = "Outro", Email = "o@x.com" };
        db.AddRange(funcao, wTec, wEng, uTec, uEng, uOutro);
        await db.SaveChangesAsync();
        uTec.TrabalhadorId = wTec.Id;
        uEng.TrabalhadorId = wEng.Id;
        await db.SaveChangesAsync();

        var estrutura = EstruturaSimplesAprovavel("Instalar linha de vida");
        estrutura.Complementares!.Tecnico = new AssinanteGeracaoPgr { UsuarioId = uTec.Id, Nome = uTec.Nome, Registro = "CFT 1" };
        estrutura.Complementares.Engenheiro = new AssinanteGeracaoPgr { UsuarioId = uEng.Id, Nome = uEng.Nome, Registro = "CREA 2" };
        var id = await CriarGeracaoAsync(db, obra, new IaFalsa());
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        var registrador = new RegistradorFalso(db);

        // Outra pessoa não assina a linha do técnico escolhido.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnviarGeracaoPgrParaAprovacaoCommandHandler(db, registrador).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id, uOutro.Id), default));
        Assert.Empty(await db.DocumentoSignatarios.ToListAsync());

        // O técnico escolhido assina ao enviar.
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db, registrador).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id, uTec.Id), default);
        var assinatura = Assert.Single(await db.DocumentoSignatarios.ToListAsync());
        Assert.Equal(PapelAssinatura.TecnicoSegurancaPgr, assinatura.Papel);
        Assert.Equal(wTec.Id, assinatura.TrabalhadorId);

        // Devolver derruba a assinatura sobre o conteúdo antigo; reenviar assina de novo, sem duplicar.
        await new DevolverGeracaoPgrCommandHandler(db).Handle(new DevolverGeracaoPgrCommand(id, "Ajustar"), default);
        Assert.Empty(await db.DocumentoSignatarios.ToListAsync());
        await new EnviarGeracaoPgrParaAprovacaoCommandHandler(db, registrador).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id, uTec.Id), default);
        Assert.Single(await db.DocumentoSignatarios.ToListAsync());

        // Só o engenheiro escolhido aprova e assina; antes disso nada fica aprovado.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AprovarGeracaoPgrCommandHandler(db, registrador).Handle(new AprovarGeracaoPgrCommand(id, uOutro.Id, "Outro"), default));
        Assert.Empty(await db.Pgrs.ToListAsync());
        await new AprovarGeracaoPgrCommandHandler(db, registrador).Handle(new AprovarGeracaoPgrCommand(id, uEng.Id, "Eng. Carlos"), default);
        Assert.Equal(new[] { PapelAssinatura.TecnicoSegurancaPgr, PapelAssinatura.EngenheiroSegurancaPgr },
            (await db.DocumentoSignatarios.Select(s => s.Papel!.Value).ToListAsync()).OrderBy(p => p).ToArray());
    }

    [Fact]
    public async Task Enviar_SemEscolherQuemAssina_OuSemRegistro_Recusa()
    {
        var (db, obra, _) = await CriarAsync();
        var estrutura = EstruturaSimplesAprovavel("Instalar linha de vida");
        estrutura.Complementares!.Tecnico = null;
        var id = await CriarGeracaoAsync(db, obra, new IaFalsa());
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default));

        estrutura.Complementares.Tecnico = new AssinanteGeracaoPgr { UsuarioId = Guid.NewGuid(), Nome = "Ana", Registro = " " };
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnviarGeracaoPgrParaAprovacaoCommandHandler(db).Handle(new EnviarGeracaoPgrParaAprovacaoCommand(id), default));
        Assert.Contains("registro profissional", ex.Message);
    }

    [Fact]
    public async Task Aprovar_Assinatura_Pgr_Aprovado_Sem_Escolha_Antiga_SegueComoAntes()
    {
        // Rascunho antigo (sem Tecnico/Engenheiro) aprovado direto no banco de dados continua aprovável.
        var (db, obra, _) = await CriarAsync();
        var estrutura = EstruturaSimplesAprovavel("Instalar linha de vida");
        estrutura.Complementares!.Tecnico = null;
        estrutura.Complementares.Engenheiro = null;
        var id = await CriarGeracaoAsync(db, obra, new IaFalsa());
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        var g = await db.GeracoesPgrEap.SingleAsync(x => x.Id == id);
        g.Status = StatusGeracaoPgrEap.AguardandoAprovacao;
        await db.SaveChangesAsync();

        var pgrId = await new AprovarGeracaoPgrCommandHandler(db).Handle(new AprovarGeracaoPgrCommand(id, null, "Eng. Carlos"), default);

        Assert.Equal(pgrId, (await db.Pgrs.SingleAsync()).Id);
    }

    [Fact]
    public async Task Aprovar_ObraSemPgrVigente_CriaPgrNovoSemRevisao()
    {
        var (db, obra, _) = await CriarAsync();
        var pgrId = await AprovarAsync(db, obra, new IaFalsa(), EstruturaSimplesAprovavel("Instalar linha de vida"));

        var pgr = await db.Pgrs.SingleAsync();
        Assert.Equal(pgrId, pgr.Id);
        Assert.Equal(StatusPgr.Vigente, pgr.Status);
        Assert.Empty(await db.PgrRevisoes.ToListAsync());
    }

    [Fact]
    public async Task Aprovar_SegundaGeracaoNaObra_ViraRevisaoDoMesmoPgr_ComPdfAnteriorArquivadoEPlanoSemDuplicar()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var existente = new Pgr
        {
            ObraId = obra.Id, Nome = "PGR da consultoria", DataElaboracao = new DateTime(2026, 1, 10), Status = StatusPgr.Vigente,
            DocumentoConteudo = pdf, DocumentoContentType = "application/pdf",
        };
        existente.PlanoDeAcao.Add(new PlanoAcaoItem { Descricao = "Instalar linha de vida", Status = StatusControleRisco.EmAndamento });
        db.Pgrs.Add(existente);
        await db.SaveChangesAsync();

        var pgrId = await AprovarAsync(db, obra, ia, EstruturaSimplesAprovavel("instalar  linha de vida"));
        var pgrId2 = await AprovarAsync(db, obra, ia, EstruturaSimplesAprovavel("Treinar equipe em altura"));

        Assert.Equal(existente.Id, pgrId);
        Assert.Equal(existente.Id, pgrId2);
        var pgr = await db.Pgrs.SingleAsync();
        Assert.Equal(StatusPgr.Vigente, pgr.Status);
        Assert.Null(pgr.DocumentoConteudo);
        var revisoes = await db.PgrRevisoes.Where(r => r.PgrId == pgr.Id).OrderBy(r => r.NumeroRevisao).ToListAsync();
        Assert.Equal(new[] { 0, 1, 2 }, revisoes.Select(r => r.NumeroRevisao).ToArray());
        Assert.Equal(pdf, revisoes[0].DocumentoConteudo);
        Assert.Null(revisoes[1].DocumentoConteudo);
        var plano = await db.PlanoAcaoItens.Where(i => i.PgrId == pgr.Id).Select(i => i.Descricao).ToListAsync();
        Assert.Equal(2, plano.Count);
        Assert.Contains("Treinar equipe em altura", plano);
        Assert.Single(await db.Ghes.Where(x => x.Ativo).ToListAsync());
    }

    [Fact]
    public async Task SugerirAcompanhamento_SoPreencheCampoVazio_NormalizaOpcoesEMarcaComoSugestaoDaIa()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        var estrutura = new EstruturaGeracaoPgr
        {
            AcoesPlano = new()
            {
                new AcaoPlanoGerada { Descricao = "Instalar linha de vida", Prazo = new DateTime(2026, 12, 1) },
                new AcaoPlanoGerada { Descricao = "Treinar ajudantes em NR-35", FormaAcompanhamento = "Auditoria", CriterioAfericao = "Digitado pelo técnico" },
                new AcaoPlanoGerada { Descricao = "   " },
                new AcaoPlanoGerada { Descricao = "Medir ruído", FormaAcompanhamento = "Medição", PeriodicidadeAcompanhamento = "Mensal", CriterioAfericao = "Abaixo de 85 dB(A)" },
            },
        };
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);
        ia.Acompanhamentos = new()
        {
            new AcompanhamentoSugeridoIa(0, "inspeção periódica", "Semanal", "100% dos pontos de ancoragem inspecionados sem não conformidade"),
            new AcompanhamentoSugeridoIa(1, "Indicador", "Quando der", "Sobrescreveria o que o técnico digitou"),
            new AcompanhamentoSugeridoIa(3, "Medição", "Mensal", "Sobrescreveria o que já estava preenchido"),
            new AcompanhamentoSugeridoIa(9, "Auditoria", "Mensal", "Índice que não existe"),
        };

        var n = await new SugerirAcompanhamentoPlanoGeracaoPgrCommandHandler(db, ia).Handle(new SugerirAcompanhamentoPlanoGeracaoPgrCommand(id), default);

        var acoes = (await new ObterGeracaoPgrEapQueryHandler(db).Handle(new ObterGeracaoPgrEapQuery(id), default)).Estrutura.AcoesPlano;
        Assert.Equal(1, n); // só a linha de vida mudou: a do treinamento não aceitou a periodicidade fora da lista
        Assert.Equal(new[] { 0, 1 }, ia.ContextoAcompanhamento!.Acoes.Select(a => a.Indice).ToArray()); // a vazia e a completa ficam de fora
        var linhaDeVida = acoes[0];
        Assert.Equal("Inspeção periódica", linhaDeVida.FormaAcompanhamento);
        Assert.Equal("Semanal", linhaDeVida.PeriodicidadeAcompanhamento);
        Assert.StartsWith("100%", linhaDeVida.CriterioAfericao);
        Assert.True(linhaDeVida.AcompanhamentoSugeridoPelaIa);
        var treinamento = acoes[1];
        Assert.Equal("Auditoria", treinamento.FormaAcompanhamento); // o técnico digitou: não muda
        Assert.Equal("Digitado pelo técnico", treinamento.CriterioAfericao);
        Assert.Null(treinamento.PeriodicidadeAcompanhamento); // "Quando der" não está na lista: fica em branco
        Assert.True(acoes[1].AcompanhamentoSugeridoPelaIa == false);
        Assert.Equal("Abaixo de 85 dB(A)", acoes[3].CriterioAfericao);
        Assert.False(acoes[3].AcompanhamentoSugeridoPelaIa);
    }

    [Fact]
    public async Task SugerirAcompanhamento_SemAcaoPendente_NaoChamaAIa()
    {
        var (db, obra, _) = await CriarAsync();
        var ia = new IaFalsa();
        var id = await CriarGeracaoAsync(db, obra, ia);
        var estrutura = new EstruturaGeracaoPgr
        {
            AcoesPlano = new() { new AcaoPlanoGerada { Descricao = "Medir ruído", FormaAcompanhamento = "Medição", PeriodicidadeAcompanhamento = "Mensal", CriterioAfericao = "Abaixo de 85 dB(A)" } },
        };
        await new SalvarGeracaoPgrEapCommandHandler(db).Handle(new SalvarGeracaoPgrEapCommand(id, 4, estrutura), default);

        var n = await new SugerirAcompanhamentoPlanoGeracaoPgrCommandHandler(db, ia).Handle(new SugerirAcompanhamentoPlanoGeracaoPgrCommand(id), default);

        Assert.Equal(0, n);
        Assert.Null(ia.ContextoAcompanhamento);
    }
}

internal static class SerializacaoGeracaoTeste
{
    public static EstruturaGeracaoPgr Ler(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<EstruturaGeracaoPgr>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
}
