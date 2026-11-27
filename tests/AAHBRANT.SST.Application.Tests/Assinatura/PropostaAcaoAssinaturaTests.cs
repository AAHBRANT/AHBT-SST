using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Etapa 2 do agente engenheiro da assinatura: ação irreversível só com "Aprovar" do responsável no Telegram.
public class PropostaAcaoAssinaturaTests
{
    internal sealed class TelegramFalso : ITelegramAlertaService
    {
        public List<(string Texto, IReadOnlyList<BotaoTelegram> Botoes)> ComBotoes { get; } = new();
        public List<(long Id, string Texto)> Edicoes { get; } = new();

        public Task<bool> EnviarAsync(string mensagem, CancellationToken ct = default) => Task.FromResult(true);

        public Task<long?> EnviarComBotoesAsync(string mensagem, IReadOnlyList<BotaoTelegram> botoes, CancellationToken ct = default)
        {
            ComBotoes.Add((mensagem, botoes));
            return Task.FromResult<long?>(777);
        }

        public Task EditarAsync(long mensagemId, string mensagem, CancellationToken ct = default)
        {
            Edicoes.Add((mensagemId, mensagem));
            return Task.CompletedTask;
        }

        public Task ResponderCliqueAsync(string cliqueId, string aviso, CancellationToken ct = default) => Task.CompletedTask;
        public bool UsuarioAutorizado(long usuarioTelegramId) => true;
        public Task<bool> RegistrarWebhookAsync(string url, string segredo, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        public List<PessoaFacialSemVinculoDto> Soltas { get; set; } = new();
        public List<IReadOnlyCollection<string>> Exclusoes { get; } = new();
        public Exception? Falha { get; init; }

        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct)
            => Task.FromResult(new DiagnosticoVinculosFacialDto(obraId, "Obra", "grupo", true, 1, 1 + Soltas.Count, [], Soltas, []));

        public Task<int> ExcluirPessoasSemVinculoAsync(Guid obraId, IReadOnlyCollection<string> personIds, CancellationToken ct)
        {
            if (Falha is not null) throw Falha;
            Exclusoes.Add(personIds);
            return Task.FromResult(personIds.Count);
        }

        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => Task.CompletedTask;
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => Task.CompletedTask;
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => Task.FromResult<ResultadoMigracaoModeloFacial?>(null);
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
            => throw new NotSupportedException();
    }

    private static async Task<Obra> CriarObraAsync(SstDbContext db)
    {
        var obra = new Obra { Nome = "Ponte Rio Cuiá", AzureFacePersonGroupId = "grupo" };
        db.Obras.Add(obra);
        var funcao = new Funcao { Nome = "Armador" };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(new Trabalhador { Nome = "Fabio da Silva Aquino", ObraId = obra.Id, FuncaoId = funcao.Id, AzureFacePersonId = "valido-1" });
        await db.SaveChangesAsync();
        return obra;
    }

    private static FacialFalso FacialComDuasSoltas() => new()
    {
        Soltas = [new PessoaFacialSemVinculoDto("solta-1", "Fabio da Silva  Aquino", 1), new PessoaFacialSemVinculoDto("solta-2", "Pessoa Desconhecida", 1)],
    };

    private static async Task<PropostaAcaoAssinaturaDto> ProporAsync(SstDbContext db, FacialFalso facial, TelegramFalso telegram, Guid obraId)
        => await new CriarPropostaAcaoAssinaturaCommandHandler(db, facial, telegram)
            .Handle(new CriarPropostaAcaoAssinaturaCommand(TipoAcaoAssinatura.ExcluirPessoasFaciaisSoltas, obraId, "Recusas 'sem vínculo' na fila."), CancellationToken.None);

    private static Task<string> DecidirAsync(SstDbContext db, FacialFalso facial, TelegramFalso telegram, Guid propostaId, bool aprovar)
        => new DecidirPropostaAcaoAssinaturaCommandHandler(db, facial, telegram)
            .Handle(new DecidirPropostaAcaoAssinaturaCommand(propostaId, aprovar), CancellationToken.None);

    [Fact]
    public async Task Propor_ServidorEscolheOsAlvos_EPedeAprovacaoComBotoes_SemNomes()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();

        var proposta = await ProporAsync(db, FacialComDuasSoltas(), telegram, obra.Id);

        Assert.Equal("Pendente", proposta.Status);
        Assert.Equal(2, proposta.TotalAlvos);
        Assert.True(proposta.AprovacaoPedidaNoTelegram);
        Assert.Contains("1 delas são cópias", proposta.Descricao);
        var salva = await db.PropostasAcaoAssinatura.SingleAsync();
        Assert.Equal(new[] { "solta-1", "solta-2" }, salva.ListaAlvos());
        Assert.Equal(777, salva.TelegramMensagemId);

        var (texto, botoes) = Assert.Single(telegram.ComBotoes);
        Assert.DoesNotContain("Fabio", texto);
        Assert.Contains("Recusas 'sem vínculo'", texto);
        Assert.Equal(new[] { PropostaAcaoAssinaturaRegras.DadoBotao(proposta.Id, true), PropostaAcaoAssinaturaRegras.DadoBotao(proposta.Id, false) },
            botoes.Select(b => b.Dado));
        Assert.All(botoes, b => Assert.True(b.Dado.Length <= 64)); // limite do callback_data do Telegram
    }

    [Fact]
    public async Task Propor_ComPedidoIgualAguardando_ReaproveitaSemNovaMensagem()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();
        var facial = FacialComDuasSoltas();

        var primeira = await ProporAsync(db, facial, telegram, obra.Id);
        var segunda = await ProporAsync(db, facial, telegram, obra.Id);

        Assert.Equal(primeira.Id, segunda.Id);
        Assert.True(segunda.Reaproveitada);
        Assert.Single(telegram.ComBotoes);
    }

    [Fact]
    public async Task Propor_SemPessoasSoltas_Recusa()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProporAsync(db, new FacialFalso(), new TelegramFalso(), obra.Id));
        Assert.Empty(db.PropostasAcaoAssinatura);
    }

    [Fact]
    public async Task Aprovar_ExecutaUmaVez_ComOsAlvosDaProposta_EAtualizaAMensagem()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();
        var facial = FacialComDuasSoltas();
        var proposta = await ProporAsync(db, facial, telegram, obra.Id);

        Assert.Equal("Feito.", await DecidirAsync(db, facial, telegram, proposta.Id, aprovar: true));
        var repetido = await DecidirAsync(db, facial, telegram, proposta.Id, aprovar: true);

        var exclusao = Assert.Single(facial.Exclusoes);
        Assert.Equal(new[] { "solta-1", "solta-2" }, exclusao);
        Assert.Contains("já está aprovado e executado", repetido);
        var salva = await db.PropostasAcaoAssinatura.SingleAsync();
        Assert.Equal(StatusPropostaAcaoAssinatura.Executada, salva.Status);
        Assert.NotNull(salva.DecididaEm);
        Assert.Contains("2 de 2", salva.Resultado);
        var (id, texto) = Assert.Single(telegram.Edicoes);
        Assert.Equal(777, id);
        Assert.Contains("✅ Aprovado", texto);
    }

    [Fact]
    public async Task Recusar_NaoExecutaNada()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();
        var facial = FacialComDuasSoltas();
        var proposta = await ProporAsync(db, facial, telegram, obra.Id);

        await DecidirAsync(db, facial, telegram, proposta.Id, aprovar: false);

        Assert.Empty(facial.Exclusoes);
        Assert.Equal(StatusPropostaAcaoAssinatura.Recusada, (await db.PropostasAcaoAssinatura.SingleAsync()).Status);
        Assert.Contains("Recusado", Assert.Single(telegram.Edicoes).Texto);
    }

    [Fact]
    public async Task AprovarDepoisDe24h_Expira_ENaoExecuta()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();
        var facial = FacialComDuasSoltas();
        var proposta = await ProporAsync(db, facial, telegram, obra.Id);
        var salva = await db.PropostasAcaoAssinatura.SingleAsync();
        salva.ExpiraEm = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        Assert.Equal("Este pedido expirou.", await DecidirAsync(db, facial, telegram, proposta.Id, aprovar: true));
        Assert.Empty(facial.Exclusoes);
        Assert.Equal(StatusPropostaAcaoAssinatura.Expirada, salva.Status);
    }

    [Fact]
    public async Task AprovarComFalhaNoAzure_RegistraFalhou()
    {
        var db = DbContextFactory.Criar();
        var obra = await CriarObraAsync(db);
        var telegram = new TelegramFalso();
        var proposta = await ProporAsync(db, FacialComDuasSoltas(), telegram, obra.Id);
        var facialQueFalha = new FacialFalso { Falha = new InvalidOperationException("Azure fora do ar.") };

        await DecidirAsync(db, facialQueFalha, telegram, proposta.Id, aprovar: true);

        var salva = await db.PropostasAcaoAssinatura.SingleAsync();
        Assert.Equal(StatusPropostaAcaoAssinatura.Falhou, salva.Status);
        Assert.Equal("Azure fora do ar.", salva.Resultado);
    }

    [Theory]
    [InlineData("pa:0123456789abcdef0123456789abcdef:s", true, true)]
    [InlineData("pa:0123456789abcdef0123456789abcdef:n", true, false)]
    [InlineData("pa:0123456789abcdef0123456789abcdef:x", false, false)]
    [InlineData("pa:nao-e-guid:s", false, false)]
    [InlineData("outro:0123456789abcdef0123456789abcdef:s", false, false)]
    [InlineData(null, false, false)]
    public void LerBotao_SoAceitaOFormatoDaProposta(string? dado, bool valido, bool aprovar)
    {
        var lido = PropostaAcaoAssinaturaRegras.LerBotao(dado);

        Assert.Equal(valido, lido is not null);
        if (lido is { } botao)
            Assert.Equal(aprovar, botao.Aprovar);
    }
}
