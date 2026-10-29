using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// Plano de emergência por mapa e IA (24/10/2026): o mapa devolve os locais reais; a IA só escolhe e justifica.
public class EmergenciaGeracaoPgrTests
{
    private sealed class MapaFalso : IProvedorMapaEmergencia
    {
        public bool AchaEnderecoCompleto { get; set; } = true;
        public bool AchaCidade { get; set; } = true;
        // Põe o hospital de urgência muito longe (mais de 10 min além do mais rápido).
        public bool UrgenciaLonge { get; set; }
        public List<string> Consultas { get; } = new();

        public string Fonte => "MapaTeste";

        public Task<LocalGeocodificado?> LocalizarAsync(string consulta, CancellationToken ct)
        {
            Consultas.Add(consulta);
            var completo = consulta.StartsWith("Rua da Obra");
            var achou = completo ? AchaEnderecoCompleto : AchaCidade;
            return Task.FromResult<LocalGeocodificado?>(achou ? new LocalGeocodificado(new PontoGeografico(-19.9, -43.9), consulta, false) : null);
        }

        public Task<List<LocalEmergenciaMapa>> BuscarProximosAsync(PontoGeografico centro, int raioMetros, CancellationToken ct) =>
            Task.FromResult(new List<LocalEmergenciaMapa>
            {
                new("node/1", TipoLocalEmergencia.Hospital, "Hospital Perto", "Rua A, 10", null, false, new PontoGeografico(-19.91, -43.9), 1.2),
                new("node/2", TipoLocalEmergencia.Hospital, "Hospital Pronto-Socorro", "Av. B, 200", "(31) 3333-0000", true, new PontoGeografico(UrgenciaLonge ? -20.1 : -19.94, -43.9), 5.0),
                new("node/5", TipoLocalEmergencia.Hospital, "Hospital de Olhos", "Rua D, 7", "(31) 3000-0000", false, new PontoGeografico(-19.905, -43.9), 0.5),
                new("node/3", TipoLocalEmergencia.Bombeiros, "1º Batalhão de Bombeiros", null, null, false, new PontoGeografico(-19.92, -43.9), 2.0),
                new("node/4", TipoLocalEmergencia.Policia, "Delegacia Central", "Rua C, 5", "(31) 3222-1111", false, new PontoGeografico(-19.93, -43.9), 3.0),
                new("node/6", TipoLocalEmergencia.Policia, "Superintendência da Polícia Federal", null, null, false, new PontoGeografico(-19.901, -43.9), 0.2),
                new("node/7", TipoLocalEmergencia.Bombeiros, "Diretoria de Apoio Logístico - Bombeiros", null, null, false, new PontoGeografico(-19.902, -43.9), 0.3),
            });

        // Quanto mais longe em linha reta, mais minutos: o hospital de urgência fica a 12 min, o próximo a 4.
        public Task<RotaMapa?> RotaAsync(PontoGeografico origem, PontoGeografico destino, CancellationToken ct)
        {
            var km = Math.Abs(destino.Latitude - origem.Latitude) * 111;
            return Task.FromResult<RotaMapa?>(new RotaMapa(km, (int)Math.Ceiling(km * 2.4),
                new List<string> { "Saia da obra e siga pela Rua da Obra por 300 m.", "Chegue ao destino." }));
        }
    }

    private sealed class IaDeEmergencia : IGeradorPgrIa
    {
        public SugestaoEmergenciaIa? Resposta { get; set; }
        public bool Falha { get; set; }
        public ContextoEmergenciaIa? Contexto { get; private set; }

        public Task<List<ItemEapGerado>> LerEapAsync(string textoEap, CancellationToken ct) => Task.FromResult(new List<ItemEapGerado>());
        public Task<List<GheGerado>> SugerirGhesAsync(IReadOnlyList<ItemEapGerado> atividades, IReadOnlyList<string> funcoesCadastradas, CancellationToken ct) => Task.FromResult(new List<GheGerado>());
        public Task<List<RiscoGerado>> GerarRiscosAsync(ContextoRiscosAtividade contexto, CancellationToken ct) => Task.FromResult(new List<RiscoGerado>());

        public List<AcompanhamentoSugeridoIa> Acompanhamentos { get; set; } = new();

        public ContextoAcompanhamentoPlano? ContextoAcompanhamento { get; private set; }

        public Task<List<AcompanhamentoSugeridoIa>> SugerirAcompanhamentoAsync(ContextoAcompanhamentoPlano contexto, CancellationToken ct) { ContextoAcompanhamento = contexto; return Task.FromResult(Acompanhamentos); }

        public Task<List<CboSugeridoIa>> SugerirCboAsync(IReadOnlyList<FuncaoParaCbo> funcoes, CancellationToken ct) => Task.FromResult(new List<CboSugeridoIa>());


        public Task<SugestaoEmergenciaIa> SugerirEmergenciaAsync(ContextoEmergenciaIa contexto, CancellationToken ct)
        {
            Contexto = contexto;
            if (Falha) throw new InvalidOperationException("IA fora do ar");
            return Task.FromResult(Resposta ?? new SugestaoEmergenciaIa(new List<RecomendacaoEmergenciaIa>(), new List<string>()));
        }
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Guid Id)> CriarAsync(string? endereco = "Rua da Obra, 100")
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste", Endereco = endereco, Cidade = "Belo Horizonte", Uf = "MG" };
        var g = new GeracaoPgrEap { Obra = obra, Nome = "PGR", DataElaboracao = DateTime.UtcNow, NomeArquivoEap = "eap.xlsx", Status = StatusGeracaoPgrEap.Rascunho };
        db.AddRange(obra, g);
        await db.SaveChangesAsync();
        return (db, g.Id);
    }

    [Fact]
    public async Task Sugerir_UsaRecomendacaoDaIa_ColocaTelefoneSoDoMapa_EIgnoraIdInventado()
    {
        var (db, id) = await CriarAsync();
        var ia = new IaDeEmergencia
        {
            Resposta = new SugestaoEmergenciaIa(
                new List<RecomendacaoEmergenciaIa>
                {
                    new("node/2", "Atende urgência e fica a 12 min."),
                    new("node/999", "Local que a IA inventou."),
                },
                new List<string> { "Resgate em trabalho em altura", "Cenário inventado" }),
        };

        var r = await new SugerirEmergenciaGeracaoPgrCommandHandler(db, new MapaFalso(), ia).Handle(new SugerirEmergenciaGeracaoPgrCommand(id), default);

        Assert.True(r.UsouIa);
        Assert.Equal(4, r.Opcoes.Count); // nenhum local a mais: o id inventado não vira opção
        var hospitalRecomendado = Assert.Single(r.Opcoes, o => o.Chave == "hospital" && o.Recomendado);
        Assert.Equal("Hospital Pronto-Socorro", hospitalRecomendado.Nome); // IA preferiu o de urgência, mesmo mais longe
        Assert.Equal("(31) 3333-0000", hospitalRecomendado.Telefone);
        Assert.Equal("Atende urgência e fica a 12 min.", hospitalRecomendado.Justificativa);
        Assert.Equal("Saia da obra e siga pela Rua da Obra por 300 m.\nChegue ao destino.", hospitalRecomendado.Rota); // caminho vem do mapa
        Assert.Null(r.Opcoes.Single(o => o.Id == "node/1").Telefone); // sem telefone no mapa continua sem telefone
        Assert.Contains(r.Avisos, a => a.Contains("telefone"));
        Assert.Equal(new[] { "Resgate em trabalho em altura" }, r.CenariosSugeridos); // cenário fora da lista é descartado
        Assert.Contains(ia.Contexto!.Candidatos, c => c.Id == "node/2" && c.AtendeUrgencia);
    }

    [Fact]
    public async Task Sugerir_SemIa_PreferePronto_SocorroQuandoChegaEmAte10MinAMais_EAvisa()
    {
        var (db, id) = await CriarAsync();

        var r = await new SugerirEmergenciaGeracaoPgrCommandHandler(db, new MapaFalso(), new IaDeEmergencia { Falha = true })
            .Handle(new SugerirEmergenciaGeracaoPgrCommand(id), default);

        Assert.False(r.UsouIa);
        // Hospital Perto leva 3 min e o pronto-socorro 11 min (8 a mais): o de urgência vence.
        Assert.Equal("Hospital Pronto-Socorro", r.Opcoes.Single(o => o.Chave == "hospital" && o.Recomendado).Nome);
        Assert.Contains(r.Avisos, a => a.Contains("IA"));
        Assert.Equal(3, r.Opcoes.Count(o => o.Recomendado)); // um por tipo
        Assert.DoesNotContain(r.Opcoes, o => o.Nome == "Hospital de Olhos"); // especialidade sem pronto-socorro fica de fora
        Assert.DoesNotContain(r.Opcoes, o => o.Nome.Contains("Federal")); // PF não atende emergência pelo 190
        Assert.DoesNotContain(r.Opcoes, o => o.Nome.Contains("Diretoria")); // órgão administrativo não é posto de bombeiros
    }

    [Fact]
    public async Task Sugerir_SemIa_ComUrgenciaMuitoLonge_RecomendaOMaisRapido()
    {
        var (db, id) = await CriarAsync();

        var r = await new SugerirEmergenciaGeracaoPgrCommandHandler(db, new MapaFalso { UrgenciaLonge = true }, new IaDeEmergencia { Falha = true })
            .Handle(new SugerirEmergenciaGeracaoPgrCommand(id), default);

        Assert.Equal("Hospital Perto", r.Opcoes.Single(o => o.Chave == "hospital" && o.Recomendado).Nome);
    }

    [Fact]
    public async Task Sugerir_EnderecoNaoAchado_CaiParaACidade_ComAvisoDeAproximado()
    {
        var (db, id) = await CriarAsync();
        var mapa = new MapaFalso { AchaEnderecoCompleto = false };

        var r = await new SugerirEmergenciaGeracaoPgrCommandHandler(db, mapa, new IaDeEmergencia()).Handle(new SugerirEmergenciaGeracaoPgrCommand(id), default);

        Assert.True(r.LocalizacaoAproximada);
        Assert.Contains(r.Avisos, a => a.Contains("centro da cidade"));
        Assert.Equal(2, mapa.Consultas.Count);
    }

    [Fact]
    public async Task Sugerir_ObraSemEnderecoNemCidade_OuMapaSemResposta_Falha()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-02", Nome = "Sem endereço" };
        var g = new GeracaoPgrEap { Obra = obra, Nome = "PGR", DataElaboracao = DateTime.UtcNow, NomeArquivoEap = "e.xlsx", Status = StatusGeracaoPgrEap.Rascunho };
        db.AddRange(obra, g);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SugerirEmergenciaGeracaoPgrCommandHandler(db, new MapaFalso(), new IaDeEmergencia()).Handle(new SugerirEmergenciaGeracaoPgrCommand(g.Id), default));

        var (db2, id2) = await CriarAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SugerirEmergenciaGeracaoPgrCommandHandler(db2, new MapaFalso { AchaEnderecoCompleto = false, AchaCidade = false }, new IaDeEmergencia())
                .Handle(new SugerirEmergenciaGeracaoPgrCommand(id2), default));
    }
}
