using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ideias;
using AAHBRANT.SST.Application.Ideias.Commands;
using AAHBRANT.SST.Application.Ideias.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Application.Tests.Ideias;

public class IdeiasHandlersTests
{
    private static readonly AutorIdeia Rafaela = new(null, "Rafaela");
    private static readonly AutorIdeia Gestor = new(null, "Gestor");

    private static IMediator CriarMediator(string banco)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SstDbContext>(o => o.UseInMemoryDatabase(banco));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SstDbContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIdeiaEstruturacaoService, HeuristicaIdeiaEstruturacaoService>();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(RegistrarIdeiaCommand).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Registrar_GeraCodigosSequenciais_EGuardaMensagemOriginal()
    {
        var m = CriarMediator(nameof(Registrar_GeraCodigosSequenciais_EGuardaMensagemOriginal));
        const string msg = "Acho que o módulo de compras deveria avisar quando o material recebido estiver diferente do pedido.";

        var a = await m.Send(new RegistrarIdeiaCommand(msg, CanalIdeia.Web, Rafaela));
        var b = await m.Send(new RegistrarIdeiaCommand("Relatório mensal de horas por obra exportado em PDF para a diretoria.", CanalIdeia.Web, Rafaela));

        Assert.Equal("IDEIA-0001", a.Codigo);
        Assert.Equal("IDEIA-0002", b.Codigo);
        var detalhe = await m.Send(new ObterIdeiaQuery(a.Id));
        Assert.Equal(msg, detalhe.MensagemOriginal);
        Assert.Equal(StatusIdeia.NovaIdeia, detalhe.Status);
        Assert.Contains(detalhe.Historico, h => h.Tipo == TipoHistoricoIdeia.Registro);
    }

    [Fact]
    public async Task Registrar_IdeiaSemelhante_SugereVinculo_EVincularNaoApagaNada()
    {
        var m = CriarMediator(nameof(Registrar_IdeiaSemelhante_SugereVinculo_EVincularNaoApagaNada));
        var primeira = await m.Send(new RegistrarIdeiaCommand("Criar alerta para vencimento de EPI no módulo de EPI.", CanalIdeia.Web, Rafaela));
        var segunda = await m.Send(new RegistrarIdeiaCommand("O sistema deveria avisar quando um EPI estiver vencido.", CanalIdeia.Web, Rafaela));

        Assert.Equal(TipoPerguntaIdeia.Duplicidade, segunda.Pergunta);
        Assert.Equal(primeira.Codigo, segunda.IdeiaSemelhanteCodigo);

        var vinculada = await m.Send(new VincularIdeiaCommand(segunda.Id, primeira.Id, Gestor));
        Assert.Equal(primeira.Id, vinculada.IdeiaPrincipalId);
        var principal = await m.Send(new ObterIdeiaQuery(primeira.Id));
        Assert.Single(principal.IdeiasVinculadas);
        Assert.Contains(principal.Comentarios, c => c.Texto.Contains(segunda.Codigo));
        // Dashboard não conta a duplicada duas vezes.
        Assert.Equal(1, (await m.Send(new DashboardIdeiasQuery())).Total);
    }

    [Fact]
    public async Task Telegram_RegistraPerguntaModulo_ERespostaDefineModulo()
    {
        var m = CriarMediator(nameof(Telegram_RegistraPerguntaModulo_ERespostaDefineModulo));
        var r1 = await m.Send(new ProcessarMensagemTelegramIdeiaCommand(10, 100, null, 7, "Rafaela Souza",
            "Gostaria que a tela ficasse mais organizada para facilitar o dia a dia."));
        Assert.Contains("Ideia registrada!", r1.Texto);
        Assert.Contains("IDEIA-0001", r1.Texto);
        Assert.True(r1.AguardaResposta);

        await m.Send(new RegistrarMensagemPerguntaIdeiaCommand(r1.IdeiaId!.Value, 555));
        var r2 = await m.Send(new ProcessarMensagemTelegramIdeiaCommand(10, 101, 555, 7, "Rafaela Souza", "EPI"));

        var ideia = await m.Send(new ObterIdeiaQuery(r1.IdeiaId.Value));
        Assert.Equal("EPI", ideia.Modulo);
        Assert.Contains("EPI", r2.Texto);
        Assert.Equal("Telegram", ideia.Canal.ToString());
    }

    [Fact]
    public async Task Telegram_Ajuda_EStatus()
    {
        var m = CriarMediator(nameof(Telegram_Ajuda_EStatus));
        var ajuda = await m.Send(new ProcessarMensagemTelegramIdeiaCommand(10, 1, null, 7, "X", "/ajuda"));
        Assert.Contains("Banco de Ideias", ajuda.Texto);
        var curta = await m.Send(new ProcessarMensagemTelegramIdeiaCommand(10, 2, null, 7, "X", "oi"));
        Assert.Contains("Conte um pouco mais", curta.Texto);
        var inexistente = await m.Send(new ProcessarMensagemTelegramIdeiaCommand(10, 3, null, 7, "X", "/status IDEIA-9999"));
        Assert.Contains("Não encontrei", inexistente.Texto);
    }

    [Fact]
    public async Task Status_FluxoCompleto_ValidaRegrasEGeraRequisitoEDemanda()
    {
        var m = CriarMediator(nameof(Status_FluxoCompleto_ValidaRegrasEGeraRequisitoEDemanda));
        var r = await m.Send(new RegistrarIdeiaCommand("Alertar no módulo de compras quando o material recebido divergir do pedido.", CanalIdeia.Web, Rafaela));

        // Não pode pular direto para Aprovada.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Aprovada, null, null, Gestor)));

        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.EmAnalise, null, null, Rafaela));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.AguardandoDecisao, null, null, Rafaela));
        // Requisito exige ideia aprovada.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.Send(new CriarRequisitoIdeiaCommand(r.Id, "t", "d", "c", Rafaela)));
        var aprovada = await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Aprovada, "Alinhado com a diretoria", null, Gestor));
        Assert.Equal(DecisaoIdeia.Aprovada, aprovada.Decisao);
        Assert.NotNull(aprovada.DataAprovacaoUtc);

        // Priorizar exige prioridade.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Priorizada, null, null, Gestor)));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Priorizada, null, PrioridadeIdeia.P1, Gestor));

        var req = await m.Send(new CriarRequisitoIdeiaCommand(r.Id, "Alerta de divergência", "O sistema deverá alertar...", "Alerta aparece em até 1 min", Rafaela));
        // Demanda só a partir de requisito aprovado.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.Send(new CriarDemandaDesenvolvimentoCommand(req.Id, null, null, null, "Dev", Gestor)));
        await m.Send(new AprovarRequisitoIdeiaCommand(req.Id, Gestor));
        var demanda = await m.Send(new CriarDemandaDesenvolvimentoCommand(req.Id, null, null, null, "Dev", Gestor));
        Assert.Equal("DEM-0001", demanda.Codigo);

        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.EmDesenvolvimento, null, null, Rafaela));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.EmTesteValidacao, null, null, Rafaela));
        var final = await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Implantada, null, null, Rafaela));
        Assert.NotNull(final.DataImplantacaoUtc);
        Assert.Single(final.Requisitos);
        Assert.Single(final.Requisitos[0].Demandas);
        Assert.Empty(final.ProximosStatus);
        // Histórico preserva toda a trilha.
        Assert.True(final.Historico.Count >= 10);
    }

    [Fact]
    public async Task Descartar_ExigeJustificativa()
    {
        var m = CriarMediator(nameof(Descartar_ExigeJustificativa));
        var r = await m.Send(new RegistrarIdeiaCommand("Trocar todo o sistema por outro fornecedor de software imediatamente.", CanalIdeia.Web, Rafaela));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Descartada, " ", null, Gestor)));
        var ok = await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Descartada, "Fora do escopo", null, Gestor));
        Assert.Equal(DecisaoIdeia.Reprovada, ok.Decisao);
    }

    [Fact]
    public async Task Busca_EncontraPorRequisito_EAnexoRespeitaLimite()
    {
        var m = CriarMediator(nameof(Busca_EncontraPorRequisito_EAnexoRespeitaLimite));
        var r = await m.Send(new RegistrarIdeiaCommand("Melhorar o fluxo de aprovação de documentos da obra para ficar mais rápido.", CanalIdeia.Web, Rafaela));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.EmAnalise, null, null, Rafaela));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.AguardandoDecisao, null, null, Rafaela));
        await m.Send(new AlterarStatusIdeiaCommand(r.Id, StatusIdeia.Aprovada, null, null, Gestor));
        await m.Send(new CriarRequisitoIdeiaCommand(r.Id, "Assinatura em lote", "O sistema deverá permitir assinar vários documentos", "Assina 10 de uma vez", Rafaela));

        Assert.Single(await m.Send(new ListarIdeiasQuery(Busca: "assinar vários")));
        Assert.Empty(await m.Send(new ListarIdeiasQuery(Busca: "inexistente xyz")));

        var anexo = await m.Send(new AnexarArquivoIdeiaCommand(r.Id, "..\\fluxo.png", "image/png", new byte[] { 1, 2, 3 }, Rafaela));
        Assert.Equal("fluxo.png", anexo.NomeArquivo);
        var baixado = await m.Send(new ObterAnexoIdeiaQuery(r.Id, anexo.Id));
        Assert.Equal(new byte[] { 1, 2, 3 }, baixado.Conteudo);
    }
}
