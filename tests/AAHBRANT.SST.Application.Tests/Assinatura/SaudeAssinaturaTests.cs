using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Relatório de saúde da assinatura e vigia do Worker (27/11: o Azure recusou o endereço do recurso às 22h e a
// obra só descobriu no DDS das 6h58).
public class SaudeAssinaturaTests
{
    private static readonly VerificacaoServicoFacialDto ServicoOk = new(true, "sst-face.cognitiveservices.azure.com", 200, null, null, 80);
    private static readonly VerificacaoServicoFacialDto ServicoFora = new(false, "sst-face.cognitiveservices.azure.com", 400, "BadRequest",
        "Please use the regional endpoint or create a custom domain for the account.", 90);

    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        public VerificacaoServicoFacialDto Servico { get; init; } = ServicoOk;
        public Func<Guid, DiagnosticoVinculosFacialDto>? Diagnostico { get; init; }
        public TreinoGrupoFacialDto? Treino { get; init; } = new("succeeded", new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc), null);
        public int Diagnosticos { get; private set; }

        public Task<VerificacaoServicoFacialDto> VerificarServicoAsync(CancellationToken ct) => Task.FromResult(Servico);
        public Task<TreinoGrupoFacialDto?> ObterTreinoGrupoAsync(Guid obraId, CancellationToken ct) => Task.FromResult(Treino);

        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct)
        {
            Diagnosticos++;
            return Task.FromResult(Diagnostico?.Invoke(obraId)
                ?? new DiagnosticoVinculosFacialDto(obraId, "Obra", "grupo", true, 0, 0, [], [], []));
        }

        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => Task.CompletedTask;
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => Task.CompletedTask;
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => Task.FromResult<ResultadoMigracaoModeloFacial?>(null);
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
            => throw new NotSupportedException();
    }

    private static async Task<(Obra Obra, Trabalhador Trabalhador)> CriarObraComFacialAsync(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db)
    {
        var obra = new Obra { Nome = "Ponte Rio Cuiá", AzureFacePersonGroupId = "obra-ponte-r4" };
        db.Obras.Add(obra);
        var funcao = new Funcao { Nome = "Armador" };
        db.Funcoes.Add(funcao);
        var trabalhador = new Trabalhador { Nome = "Fulano de Tal", ObraId = obra.Id, FuncaoId = funcao.Id, AzureFacePersonId = "person-1" };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        return (obra, trabalhador);
    }

    [Fact]
    public async Task Saude_ServicoFora_ECritico_ENaoConsultaAsObras()
    {
        var db = DbContextFactory.Criar();
        await CriarObraComFacialAsync(db);
        var facial = new FacialFalso { Servico = ServicoFora };

        var saude = await new ObterSaudeAssinaturaQueryHandler(db, facial).Handle(new ObterSaudeAssinaturaQuery(), CancellationToken.None);

        Assert.Equal("critico", saude.Situacao);
        var alerta = Assert.Single(saude.Alertas);
        Assert.Equal("SERVICO_FACIAL_FORA", alerta.Codigo);
        Assert.Equal(SeveridadeAlertaAssinatura.Critico, alerta.Severidade);
        Assert.Contains("regional endpoint", alerta.Mensagem);
        Assert.Equal(0, facial.Diagnosticos);
    }

    [Fact]
    public async Task Saude_PessoasSoltasRecusasELeitorParado_ViramAlertasDeAtencao()
    {
        var db = DbContextFactory.Criar();
        var (obra, trabalhador) = await CriarObraComFacialAsync(db);
        var agora = DateTime.UtcNow;
        for (var i = 0; i < ObterSaudeAssinaturaQueryHandler.LimiteFalhas24h; i++)
            db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial
            {
                ObraId = obra.Id,
                Motivo = i % 2 == 0 ? MotivoFalhaFacial.RostoAmbiguo : MotivoFalhaFacial.CadastroSemVinculo,
                OcorridaEm = agora.AddHours(-1),
            });
        // Fora da janela de 24 h: não conta.
        db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial { ObraId = obra.Id, Motivo = MotivoFalhaFacial.ConfiancaBaixa, OcorridaEm = agora.AddDays(-2) });
        db.DispositivosAgenteBiometrico.Add(new DispositivoAgenteBiometrico
        {
            ObraId = obra.Id, Nome = "PC do almoxarifado", SegredoHash = "x", UltimaSincronizacaoEm = agora.AddDays(-5),
        });
        await db.SaveChangesAsync();

        var facial = new FacialFalso
        {
            Diagnostico = id => new DiagnosticoVinculosFacialDto(id, obra.Nome, obra.AzureFacePersonGroupId, true, 1, 3, [],
                [new PessoaFacialSemVinculoDto("solta-1", trabalhador.Nome, 1), new PessoaFacialSemVinculoDto("solta-2", trabalhador.Nome, 1)], []),
        };

        var saude = await new ObterSaudeAssinaturaQueryHandler(db, facial).Handle(new ObterSaudeAssinaturaQuery(), CancellationToken.None);

        Assert.Equal("atencao", saude.Situacao);
        Assert.Equal(
            new[] { "LEITOR_DIGITAL_SEM_CONTATO", "MUITAS_RECUSAS_FACIAIS", "PESSOAS_FACIAIS_SOLTAS" },
            saude.Alertas.Select(a => a.Codigo).OrderBy(c => c).ToArray());
        var obraSaude = Assert.Single(saude.Obras);
        Assert.Equal(ObterSaudeAssinaturaQueryHandler.LimiteFalhas24h, obraSaude.Falhas24h.Total);
        Assert.Equal(5, obraSaude.Falhas24h.RostoAmbiguo);
        Assert.Equal(2, obraSaude.PessoasSemVinculo);
        Assert.Equal(1, obraSaude.TrabalhadoresAtivos);
        Assert.Equal(1, obraSaude.SemCadastroFacial); // sem foto de cadastro guardada
        Assert.All(saude.Alertas, a => Assert.Equal(obra.Id, a.ObraId));

        // LGPD: o relatório sai do sistema para o agente — nada de nome de funcionário.
        Assert.DoesNotContain(trabalhador.Nome, JsonSerializer.Serialize(saude));
    }

    [Fact]
    public async Task Saude_TudoCerto_ESituacaoOk()
    {
        var db = DbContextFactory.Criar();
        await CriarObraComFacialAsync(db);

        var saude = await new ObterSaudeAssinaturaQueryHandler(db, new FacialFalso()).Handle(new ObterSaudeAssinaturaQuery(), CancellationToken.None);

        Assert.Equal("ok", saude.Situacao);
        Assert.Empty(saude.Alertas);
        Assert.Equal("succeeded", Assert.Single(saude.Obras).TreinoStatus);
    }

    [Fact]
    public async Task Saude_EnderecoRegional_EAtencao()
    {
        var db = DbContextFactory.Criar();
        var facial = new FacialFalso { Servico = ServicoOk with { Endpoint = "eastus.api.cognitive.microsoft.com" } };

        var saude = await new ObterSaudeAssinaturaQueryHandler(db, facial).Handle(new ObterSaudeAssinaturaQuery(), CancellationToken.None);

        Assert.Equal("ENDPOINT_FACIAL_REGIONAL", Assert.Single(saude.Alertas).Codigo);
    }

    [Fact]
    public void Vigia_AvisaAQueda_UmaVez_LembraADuasHoras_EAvisaAVolta()
    {
        var vigia = new VigiaAssinatura();
        var t0 = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(DecisaoVigiaAssinatura.Nada, vigia.Avaliar(ServicoOk, t0));
        Assert.Equal(DecisaoVigiaAssinatura.AvisarQueda, vigia.Avaliar(ServicoFora, t0.AddMinutes(10)));
        Assert.Equal(DecisaoVigiaAssinatura.Nada, vigia.Avaliar(ServicoFora, t0.AddMinutes(20)));
        Assert.Equal(DecisaoVigiaAssinatura.Nada, vigia.Avaliar(ServicoFora, t0.AddMinutes(10) + VigiaAssinatura.IntervaloLembrete - TimeSpan.FromMinutes(1)));
        Assert.Equal(DecisaoVigiaAssinatura.LembrarQueda, vigia.Avaliar(ServicoFora, t0.AddMinutes(10) + VigiaAssinatura.IntervaloLembrete));
        Assert.True(vigia.Fora);
        Assert.Equal(DecisaoVigiaAssinatura.AvisarVolta, vigia.Avaliar(ServicoOk, t0.AddHours(5)));
        Assert.False(vigia.Fora);
        Assert.Equal(DecisaoVigiaAssinatura.Nada, vigia.Avaliar(ServicoOk, t0.AddHours(6)));
    }

    [Fact]
    public void Vigia_MensagemDeQueda_TemHoraDeBrasiliaEOErroDoAzure()
    {
        var vigia = new VigiaAssinatura();
        var queda = new DateTime(2026, 11, 27, 1, 5, 0, DateTimeKind.Utc); // 22:05 de 26/11 em Brasília
        var decisao = vigia.Avaliar(ServicoFora, queda);

        var mensagem = vigia.Mensagem(decisao, ServicoFora, queda, agenteChamado: true);

        Assert.Contains("26/11 22:05", mensagem);
        Assert.Contains("BadRequest", mensagem);
        Assert.Contains("digital", mensagem);
        Assert.Contains("agente engenheiro foi chamado", mensagem);
    }
}
