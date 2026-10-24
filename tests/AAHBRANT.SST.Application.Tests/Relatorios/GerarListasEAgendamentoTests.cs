using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class GerarListasEAgendamentoTests
{
    private sealed class ImagemSimulada : IImagemListaPresencaService
    {
        public byte[] Gerar(ListaPresencaDados dados) => new byte[] { 0x89, 0x50, 0x4E, 0x47 };
    }

    private sealed class PublicadorSimulado : IPublicadorRelatorio
    {
        public List<NovoRelatorio> Publicados { get; } = new();
        public Task<Guid?> PublicarAsync(NovoRelatorio relatorio, CancellationToken ct = default)
        {
            Publicados.Add(relatorio);
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private static ListaPresencaDados Dados(Guid ddsId, int total = 5) => new(
        ddsId, "Obra Sul", new DateTime(2026, 10, 7), null, new DateTime(2026, 10, 8, 8, 0, 0), total,
        Enumerable.Range(0, total).Select(i => new PresencaLinha($"P{i}", $"{i}", null)).ToList(), Array.Empty<AusenteLinha>(),
        null, null, null, null, null);

    private static async Task<(SstDbContext Db, Obra Obra, Guid ResponsavelId)> NovoBanco()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var usuario = new Usuario { Nome = "R", Email = "r@example.test" };
        db.AddRange(obra, usuario);
        await db.SaveChangesAsync();
        return (db, obra, usuario.Id);
    }

    private static DdsEntidade NovoDds(Obra obra, Guid responsavel, DateTime data, StatusDds status = StatusDds.Concluido, bool semExpediente = false) => new()
    {
        ObraId = obra.Id, ResponsavelUsuarioId = responsavel, Data = data, Status = status, SemExpediente = semExpediente,
    };

    private static GerarListasDePresencaCommandHandler Criar(SstDbContext db, PublicadorSimulado publicador, Func<object, object?>? responder = null) =>
        new(db,
            new MediatorSimulado(responder ?? (r => r switch
            {
                ConstruirListaPresencaQuery q => Dados(q.DdsId),
                ExportarDdsPdfQuery => new byte[] { 0x25, 0x50, 0x44, 0x46 },
                _ => null,
            })),
            new ImagemSimulada(), publicador, NullLogger<GerarListasDePresencaCommandHandler>.Instance);

    [Fact]
    public async Task PublicaSoOsDdsEncerradosComExpedienteEAindaNaoInformados()
    {
        var (db, obra, resp) = await NovoBanco();
        var hoje = FusoBrasilia.Hoje();
        var ontem = NovoDds(obra, resp, hoje.AddDays(-1));
        var jaInformado = NovoDds(obra, resp, hoje.AddDays(-1));
        var emAndamento = NovoDds(obra, resp, hoje, StatusDds.EmAndamento);
        var semExpediente = NovoDds(obra, resp, hoje.AddDays(-1), semExpediente: true);
        db.AddRange(ontem, jaInformado, emAndamento, semExpediente);
        db.RelatoriosGerados.Add(new RelatorioGerado { ChaveUnica = GerarListasDePresencaCommandHandler.ChaveDoDds(jaInformado.Id), Titulo = "x", Resumo = "x", GeradoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var publicador = new PublicadorSimulado();

        var quantos = await Criar(db, publicador).Handle(new GerarListasDePresencaCommand(), CancellationToken.None);

        Assert.Equal(1, quantos);
        var publicado = Assert.Single(publicador.Publicados);
        Assert.Equal(ontem.Id, publicado.ReferenciaId);
        Assert.Equal(obra.Id, publicado.ObraId);
        Assert.Equal(TipoRelatorio.ListaPresencaDds, publicado.Tipo);
        Assert.Equal($"dds:{ontem.Id}", publicado.ChaveUnica);
        Assert.NotNull(publicado.Pdf);
        Assert.StartsWith("Lista-de-Presenca_ObraSul_", publicado.PdfNome);
    }

    [Fact]
    public async Task APartirDeAgora_OHistoricoAntigoNaoInundaOChat()
    {
        var (db, obra, resp) = await NovoBanco();
        var hoje = FusoBrasilia.Hoje();
        var antigo = NovoDds(obra, resp, hoje.AddDays(-30));
        var anteontem = NovoDds(obra, resp, hoje.AddDays(-2));
        var ontem = NovoDds(obra, resp, hoje.AddDays(-1));
        db.AddRange(antigo, anteontem, ontem);
        await db.SaveChangesAsync();
        var publicador = new PublicadorSimulado();

        await Criar(db, publicador).Handle(new GerarListasDePresencaCommand(), CancellationToken.None);

        // Sem execução anterior, o corte é o dia anterior a hoje.
        Assert.Equal(new[] { ontem.Id }, publicador.Publicados.Select(p => p.ReferenciaId!.Value));
    }

    [Fact]
    public async Task DepoisDoPrimeiroDisparo_ODiaAnteriorAoPrimeiroDisparoContinuaValendo()
    {
        var (db, obra, resp) = await NovoBanco();
        var hoje = FusoBrasilia.Hoje();
        db.ExecucoesRelatorioAgendado.Add(new ExecucaoRelatorioAgendado { Tipo = TipoRelatorio.ListaPresencaDds, Data = hoje.AddDays(-5), ExecutadoEm = DateTime.UtcNow });
        var antesDoCorte = NovoDds(obra, resp, hoje.AddDays(-7));
        var noCorte = NovoDds(obra, resp, hoje.AddDays(-6)); // dia anterior ao primeiro disparo
        db.AddRange(antesDoCorte, noCorte);
        await db.SaveChangesAsync();
        var publicador = new PublicadorSimulado();

        await Criar(db, publicador).Handle(new GerarListasDePresencaCommand(), CancellationToken.None);

        Assert.Equal(new[] { noCorte.Id }, publicador.Publicados.Select(p => p.ReferenciaId!.Value));
    }

    [Fact]
    public async Task DdsSemNinguem_NaoGeraRelatorio()
    {
        var (db, obra, resp) = await NovoBanco();
        db.Add(NovoDds(obra, resp, FusoBrasilia.Hoje().AddDays(-1)));
        await db.SaveChangesAsync();
        var publicador = new PublicadorSimulado();

        var quantos = await Criar(db, publicador, r => r is ConstruirListaPresencaQuery q ? Dados(q.DdsId, total: 0) : new byte[] { 1 })
            .Handle(new GerarListasDePresencaCommand(), CancellationToken.None);

        Assert.Equal(0, quantos);
        Assert.Empty(publicador.Publicados);
    }

    [Fact]
    public async Task UmDdsComProblema_NaoImpedeOsOutros()
    {
        var (db, obra, resp) = await NovoBanco();
        var hoje = FusoBrasilia.Hoje();
        var quebrado = NovoDds(obra, resp, hoje.AddDays(-1));
        var bom = NovoDds(obra, resp, hoje.AddDays(-1));
        db.AddRange(quebrado, bom);
        await db.SaveChangesAsync();
        var publicador = new PublicadorSimulado();

        var quantos = await Criar(db, publicador, r => r switch
        {
            ConstruirListaPresencaQuery q when q.DdsId == quebrado.Id => throw new InvalidOperationException("dado inconsistente"),
            ConstruirListaPresencaQuery q => Dados(q.DdsId),
            _ => new byte[] { 1 },
        }).Handle(new GerarListasDePresencaCommand(), CancellationToken.None);

        Assert.Equal(1, quantos);
        Assert.Equal(bom.Id, Assert.Single(publicador.Publicados).ReferenciaId);
    }

    [Theory]
    [InlineData(1, "1 min")]
    [InlineData(39, "39 min")]
    [InlineData(60, "1h00")]
    [InlineData(112, "1h52")]
    public void FormataADuracao(int minutos, string esperado) =>
        Assert.Equal(esperado, GerarListasDePresencaCommandHandler.FormatarDuracao(minutos));

    [Fact]
    public void ResumoEmTexto_TemPresentesAusentesEDuracao()
    {
        var dados = Dados(Guid.NewGuid(), 28) with
        {
            Ausentes = new[] { new AusenteLinha("A", "1", 1), new AusenteLinha("B", "2", 3) }, DuracaoMinutos = 39,
        };

        var texto = GerarListasDePresencaCommandHandler.Resumo(dados);

        Assert.Equal("Presença do DDS, Obra Sul, quarta 07/10: 28 de 28 presentes, 2 ausentes. Duração do DDS: 39 min.", texto);
    }

    // ---- agendamento das 08:00 ----

    private static ExecutarRelatoriosAgendadosCommandHandler Agendador(SstDbContext db, MediatorSimulado mediator) => new(db, mediator);

    private static MediatorSimulado Gerador(int publicados = 3) => new(r => r is GerarListasDePresencaCommand ? publicados : null);

    private static readonly DateTime Hoje = new(2026, 10, 8);

    [Fact]
    public async Task Antes_Das_0800_NaoFazNada()
    {
        var (db, _, _) = await NovoBanco();
        var mediator = Gerador();

        var n = await Agendador(db, mediator).Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(7).AddMinutes(59)), CancellationToken.None);

        Assert.Equal(0, n);
        Assert.Empty(mediator.Requisicoes);
        Assert.Empty(db.ExecucoesRelatorioAgendado);
    }

    [Fact]
    public async Task As_0800_Dispara_EAnotaQueHojeJaFoi()
    {
        var (db, _, _) = await NovoBanco();
        var mediator = Gerador(3);

        var n = await Agendador(db, mediator).Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(8)), CancellationToken.None);

        Assert.Equal(3, n);
        var execucao = await db.ExecucoesRelatorioAgendado.SingleAsync();
        Assert.Equal((Hoje, 3, TipoRelatorio.ListaPresencaDds), (execucao.Data, execucao.RelatoriosGerados, execucao.Tipo));
    }

    [Fact]
    public async Task NoMesmoDia_NaoDisparaDeNovo()
    {
        var (db, _, _) = await NovoBanco();
        var mediator = Gerador();
        var agendador = Agendador(db, mediator);

        await agendador.Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(8).AddMinutes(1)), CancellationToken.None);
        var segunda = await agendador.Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(8).AddMinutes(6)), CancellationToken.None);

        Assert.Equal(0, segunda);
        Assert.Single(mediator.Requisicoes);
        Assert.Equal(1, await db.ExecucoesRelatorioAgendado.CountAsync());
    }

    [Fact]
    public async Task SeOWorkerEstavaForaDoAr_RecuperaOAtrasoNoMesmoDia()
    {
        var (db, _, _) = await NovoBanco();
        var mediator = Gerador(2);

        var n = await Agendador(db, mediator).Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(15)), CancellationToken.None);

        Assert.Equal(2, n); // voltou às 15:00 e ainda mandou a lista de hoje
    }

    [Fact]
    public async Task NoDiaSeguinte_DisparaDeNovo()
    {
        var (db, _, _) = await NovoBanco();
        var mediator = Gerador();
        var agendador = Agendador(db, mediator);

        await agendador.Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddHours(8)), CancellationToken.None);
        await agendador.Handle(new ExecutarRelatoriosAgendadosCommand(Hoje.AddDays(1).AddHours(8)), CancellationToken.None);

        Assert.Equal(2, mediator.Requisicoes.Count);
        Assert.Equal(2, await db.ExecucoesRelatorioAgendado.CountAsync());
    }
}
