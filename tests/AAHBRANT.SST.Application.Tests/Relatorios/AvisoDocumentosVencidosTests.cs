using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class AvisoDocumentosVencidosTests
{
    // Sexta-feira, 16/10/2026, no horário do aviso.
    private static readonly DateTime SextaAs8 = new(2026, 10, 16, 8, 0, 0);

    private sealed class EmailFalso : IEmailService
    {
        public List<(string Para, string Assunto, string Html)> Enviados { get; } = new();
        public Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
        {
            Enviados.Add((para, assunto, corpoHtml));
            return Task.CompletedTask;
        }

        public Task EnviarAsync(string para, string assunto, string corpoHtml, IReadOnlyList<AnexoEmail> anexos, CancellationToken ct = default)
            => EnviarAsync(para, assunto, corpoHtml, ct);
    }

    private sealed class FilaFalsa : IFilaNotificacaoTeams
    {
        public List<NotificacaoTeamsMensagem> Mensagens { get; } = new();
        public Task EnfileirarAsync(NotificacaoTeamsMensagem mensagem, CancellationToken ct = default)
        {
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    private sealed record Cenario(IAppDbContext Db, EmailFalso Email, FilaFalsa Fila, Obra ObraA, Obra ObraB, Pgr PgrA)
    {
        // Os mesmos providers do Motor registrados na DI (Documento incluído para provar que o PCMSO não sai repetido).
        public IAlertaOrigemProvider[] Providers() => new IAlertaOrigemProvider[]
        {
            new PgrAlertaProvider(Db), new PcmsoAlertaProvider(Db), new DocumentoAlertaProvider(Db),
            new AsoAlertaProvider(Db), new TreinamentoAlertaProvider(Db),
        };

        public EnviarAvisoDocumentosVencidosCommandHandler Handler() =>
            new(Db, Providers(), Email, Fila, NullLogger<EnviarAvisoDocumentosVencidosCommandHandler>.Instance);
    }

    private static async Task<Cenario> MontarAsync(string nomeBanco)
    {
        var db = new SstDbContext(
            new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

        var obraA = new Obra { Codigo = "A", Nome = "Parque Roger", Status = StatusObra.EmAndamento };
        var obraB = new Obra { Codigo = "B", Nome = "Obra B", Status = StatusObra.EmAndamento };
        var tecnico = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico" };
        var engenheiro = new PerfilAcesso { Tipo = TipoPerfilAcesso.EngenheiroSeguranca, Nome = "Engenheiro" };
        var gestor = new PerfilAcesso { Tipo = TipoPerfilAcesso.GestorDeObra, Nome = "Gestor" };
        var encarregado = new PerfilAcesso { Tipo = TipoPerfilAcesso.Encarregado, Nome = "Encarregado" };
        var tecA = new Usuario { Nome = "Téc A", Email = "teca@x.com" };
        var gestorA = new Usuario { Nome = "Gestor A", Email = "gestora@x.com" };
        var engEmpresa = new Usuario { Nome = "Eng Empresa", Email = "eng@x.com" };
        var tecB = new Usuario { Nome = "Téc B", Email = "tecb@x.com" };
        var encA = new Usuario { Nome = "Enc A", Email = "enca@x.com" };
        db.Obras.AddRange(obraA, obraB);
        db.PerfisAcesso.AddRange(tecnico, engenheiro, gestor, encarregado);
        db.Usuarios.AddRange(tecA, gestorA, engEmpresa, tecB, encA);
        await db.SaveChangesAsync();
        db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = tecA.Id, PerfilAcessoId = tecnico.Id, ObraId = obraA.Id },
            new UsuarioPerfilObra { UsuarioId = gestorA.Id, PerfilAcessoId = gestor.Id, ObraId = obraA.Id },
            new UsuarioPerfilObra { UsuarioId = engEmpresa.Id, PerfilAcessoId = engenheiro.Id, ObraId = null },
            new UsuarioPerfilObra { UsuarioId = tecB.Id, PerfilAcessoId = tecnico.Id, ObraId = obraB.Id },
            new UsuarioPerfilObra { UsuarioId = encA.Id, PerfilAcessoId = encarregado.Id, ObraId = obraA.Id });

        var pgrA = new Pgr { ObraId = obraA.Id, Nome = "PGR Parque Roger", Status = StatusPgr.Vigente, DataTermino = new DateTime(2026, 10, 10) };
        db.Pgrs.Add(pgrA);
        await db.SaveChangesAsync();
        return new Cenario(db, new EmailFalso(), new FilaFalsa(), obraA, obraB, pgrA);
    }

    [Fact]
    public async Task PgrVencido_AvisaTecnicoGestorDaObraEEngenheiroDaEmpresa_NaoAvisaOutraObraNemEncarregado()
    {
        var c = await MontarAsync(nameof(PgrVencido_AvisaTecnicoGestorDaObraEEngenheiroDaEmpresa_NaoAvisaOutraObraNemEncarregado));

        var enviados = await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);

        Assert.Equal(3, enviados);
        Assert.Equal(new[] { "eng@x.com", "gestora@x.com", "teca@x.com" }, c.Email.Enviados.Select(e => e.Para).OrderBy(e => e));
        var (_, assunto, html) = c.Email.Enviados[0];
        Assert.Contains("PGR vencido", assunto);
        Assert.Contains("6 dia(s)", html);
        Assert.Contains("10/10/2026", html);
        Assert.Contains("cid:logo-aahbrant", html);
        Assert.Contains("às 8h", html);
    }

    [Fact]
    public async Task SabadoOuAntesDas8_NaoEnvia()
    {
        var c = await MontarAsync(nameof(SabadoOuAntesDas8_NaoEnvia));

        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 17, 9, 0, 0)), default));
        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 16, 7, 59, 0)), default));
        Assert.Empty(c.Email.Enviados);
    }

    [Fact]
    public async Task SegundaChamadaNoMesmoDia_NaoReenvia_NoDiaSeguinteEnviaDeNovo()
    {
        var c = await MontarAsync(nameof(SegundaChamadaNoMesmoDia_NaoReenvia_NoDiaSeguinteEnviaDeNovo));

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);
        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8.AddHours(3)), default);
        Assert.Equal(3, c.Email.Enviados.Count);

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 19, 8, 5, 0)), default);
        Assert.Equal(6, c.Email.Enviados.Count);
    }

    [Fact]
    public async Task ObraEncerradaOuPgrEncerradoOuDentroDoPrazo_NaoAvisa()
    {
        var c = await MontarAsync(nameof(ObraEncerradaOuPgrEncerradoOuDentroDoPrazo_NaoAvisa));
        c.ObraA.Status = StatusObra.Encerrada;
        c.Db.Pgrs.Add(new Pgr { ObraId = c.ObraB.Id, Nome = "PGR B", Status = StatusPgr.Encerrado, DataTermino = new DateTime(2026, 1, 1) });
        c.Db.Pgrs.Add(new Pgr { ObraId = c.ObraB.Id, Nome = "PGR B novo", Status = StatusPgr.Vigente, DataTermino = new DateTime(2027, 1, 1) });
        await c.Db.SaveChangesAsync();

        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default));
    }

    [Fact]
    public async Task PcmsoVencido_EntraNoMesmoEmailDoPgr_SemRepetirPeloProviderDeDocumento()
    {
        var c = await MontarAsync(nameof(PcmsoVencido_EntraNoMesmoEmailDoPgr_SemRepetirPeloProviderDeDocumento));
        c.Db.PcmsoDetalhes.Add(new PcmsoDetalhe
        {
            Nome = "PCMSO Parque Roger", ObraId = c.ObraA.Id, Status = StatusPcmsoDocumento.Vigente,
            Validade = new DateTime(2026, 9, 30), DataEmissao = new DateTime(2025, 9, 30),
        });
        await c.Db.SaveChangesAsync();

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);

        var tecnico = Assert.Single(c.Email.Enviados, e => e.Para == "teca@x.com");
        Assert.Contains("2 itens vencidos", tecnico.Assunto);
        Assert.Contains("PCMSO Parque Roger", tecnico.Html);
        Assert.Contains("PGR Parque Roger", tecnico.Html);
    }

    [Fact]
    public async Task AsoETreinamentoVencidos_EntramNoAvisoDaObra_SemDesligadoNemRenovado()
    {
        var c = await MontarAsync(nameof(AsoETreinamentoVencidos_EntramNoAvisoDaObra_SemDesligadoNemRenovado));
        var fulano = new Trabalhador { ObraId = c.ObraA.Id, Nome = "Fulano" };
        var desligado = new Trabalhador { ObraId = c.ObraA.Id, Nome = "Desligado", Situacao = SituacaoTrabalhador.Desligado };
        var renovado = new Trabalhador { ObraId = c.ObraA.Id, Nome = "Renovado" };
        var curso = new CursoTreinamento { Nome = "NR-35", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        c.Db.Trabalhadores.AddRange(fulano, desligado, renovado);
        c.Db.CursosTreinamento.Add(curso);
        await c.Db.SaveChangesAsync();
        c.Db.Asos.AddRange(
            new Aso { TrabalhadorId = fulano.Id, DataExame = new DateTime(2025, 10, 1), DataValidade = new DateTime(2026, 10, 1) },
            new Aso { TrabalhadorId = desligado.Id, DataExame = new DateTime(2025, 10, 1), DataValidade = new DateTime(2026, 10, 1) },
            new Aso { TrabalhadorId = renovado.Id, DataExame = new DateTime(2025, 10, 1), DataValidade = new DateTime(2026, 10, 1) },
            new Aso { TrabalhadorId = renovado.Id, DataExame = new DateTime(2026, 10, 2), DataValidade = new DateTime(2027, 10, 2) });
        c.Db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = fulano.Id, CursoTreinamentoId = curso.Id, CargaHorariaRealizada = 8,
            DataRealizacao = new DateTime(2024, 10, 5), DataValidade = new DateTime(2026, 10, 5),
        });
        await c.Db.SaveChangesAsync();

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);

        var tecnico = Assert.Single(c.Email.Enviados, e => e.Para == "teca@x.com");
        Assert.Contains("3 itens vencidos", tecnico.Assunto);
        Assert.Contains("ASO de Fulano", tecnico.Html);
        Assert.Contains("NR-35 de Fulano", tecnico.Html);
        Assert.DoesNotContain("Desligado", tecnico.Html);
        Assert.DoesNotContain("Renovado", tecnico.Html);
        Assert.DoesNotContain("— validade", tecnico.Html);
        // A técnica da outra obra não recebe nada da obra A.
        Assert.DoesNotContain(c.Email.Enviados, e => e.Para == "tecb@x.com");
    }

    [Fact]
    public async Task TecnicoComDuasObras_RecebeUmSoAvisoComAsDuas()
    {
        var c = await MontarAsync(nameof(TecnicoComDuasObras_RecebeUmSoAvisoComAsDuas));
        var tecA = c.Db.Usuarios.Single(u => u.Email == "teca@x.com");
        var perfilTecnico = c.Db.PerfisAcesso.Single(p => p.Tipo == TipoPerfilAcesso.TecnicoSeguranca);
        c.Db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = tecA.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = c.ObraB.Id });
        c.Db.Pgrs.Add(new Pgr { ObraId = c.ObraB.Id, Nome = "PGR Obra B", Status = StatusPgr.Vigente, DataTermino = new DateTime(2026, 10, 1) });
        await c.Db.SaveChangesAsync();

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);

        var tecnicoA = Assert.Single(c.Email.Enviados, e => e.Para == "teca@x.com");
        Assert.Contains("Obra Parque Roger", tecnicoA.Html);
        Assert.Contains("Obra Obra B", tecnicoA.Html);
        var tecnicoB = Assert.Single(c.Email.Enviados, e => e.Para == "tecb@x.com");
        Assert.Contains("PGR Obra B", tecnicoB.Html);
        Assert.DoesNotContain("Parque Roger", tecnicoB.Html);
    }

    [Fact]
    public async Task ComAlertaDoMotorEmAberto_EnfileiraSininhoParaCadaResponsavel()
    {
        var c = await MontarAsync(nameof(ComAlertaDoMotorEmAberto_EnfileiraSininhoParaCadaResponsavel));
        var alerta = new Alerta
        {
            Tipo = TipoAlerta.PgrVencido, Severidade = SeveridadeAlerta.Critico, Titulo = "VENCIDO",
            EntidadeOrigemTipo = "PgrTermino", EntidadeOrigemId = c.PgrA.Id, ObraId = c.ObraA.Id,
        };
        c.Db.Alertas.Add(alerta);
        await c.Db.SaveChangesAsync();

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs8), default);

        Assert.Equal(3, c.Fila.Mensagens.Count);
        Assert.All(c.Fila.Mensagens, m => Assert.Equal(alerta.Id, m.AlertaId));
    }

    [Fact]
    public async Task PcmsoAlertaProvider_IgnoraRascunhoEObsoleto()
    {
        var c = await MontarAsync(nameof(PcmsoAlertaProvider_IgnoraRascunhoEObsoleto));
        c.Db.PcmsoDetalhes.AddRange(
            new PcmsoDetalhe { Nome = "Vigente", Status = StatusPcmsoDocumento.Vigente, Validade = new DateTime(2026, 9, 1) },
            new PcmsoDetalhe { Nome = "Rascunho", Status = StatusPcmsoDocumento.Rascunho, Validade = new DateTime(2026, 9, 1) },
            new PcmsoDetalhe { Nome = "Obsoleto", Status = StatusPcmsoDocumento.Obsoleto, Validade = new DateTime(2026, 9, 1) });
        await c.Db.SaveChangesAsync();

        var itens = await new PcmsoAlertaProvider(c.Db).ObterItensAsync();

        var item = Assert.Single(itens);
        Assert.Equal(TipoAlerta.PcmsoVencido, item.TipoAlertaVencido);
        Assert.Contains("Vigente", item.Titulo);
    }

    [Theory]
    [InlineData("2026-10-19 07:59", "2026-10-19")] // segunda antes das 8h: sai hoje
    [InlineData("2026-10-19 08:00", "2026-10-20")] // segunda depois das 8h: terça
    [InlineData("2026-10-16 10:00", "2026-10-19")] // sexta depois das 8h: segunda
    [InlineData("2026-10-17 09:00", "2026-10-19")] // sábado: segunda
    public void ProximoEnvio_PulaFimDeSemanaEHorarioJaPassado(string agora, string esperado)
    {
        Assert.Equal(DateTime.Parse(esperado), PreviaAvisoDocumentosVencidosQueryHandler.ProximoEnvio(DateTime.Parse(agora)));
    }

    [Fact]
    public async Task Previa_ListaDestinatariosComMotivoESininho_SemEnviarNada()
    {
        var c = await MontarAsync(nameof(Previa_ListaDestinatariosComMotivoESininho_SemEnviarNada));

        var previa = await new PreviaAvisoDocumentosVencidosQueryHandler(c.Db, c.Providers())
            .Handle(new PreviaAvisoDocumentosVencidosQuery(SextaAs8), default);

        Assert.Equal(new DateTime(2026, 10, 19), previa.DataEnvio);
        Assert.Equal(new[] { "Eng Empresa", "Gestor A", "Téc A" }, previa.Destinatarios.Select(d => d.Nome));
        var tecnico = previa.Destinatarios.Single(d => d.Nome == "Téc A");
        Assert.Equal(new[] { "Técnico de Segurança — Parque Roger" }, tecnico.Motivos);
        Assert.Equal("Engenheiro de Segurança da empresa (todas as obras)", previa.Destinatarios.Single(d => d.Nome == "Eng Empresa").Motivos.Single());
        Assert.False(tecnico.RecebeSininho);
        Assert.Equal("PGR", Assert.Single(tecnico.Itens).Tipo);
        Assert.Empty(c.Email.Enviados);
        Assert.Empty(c.Db.ExecucoesRelatorioAgendado);

        c.Db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.PgrVencido, Severidade = SeveridadeAlerta.Critico, Titulo = "VENCIDO",
            EntidadeOrigemTipo = "PgrTermino", EntidadeOrigemId = c.PgrA.Id, ObraId = c.ObraA.Id,
        });
        await c.Db.SaveChangesAsync();

        previa = await new PreviaAvisoDocumentosVencidosQueryHandler(c.Db, c.Providers())
            .Handle(new PreviaAvisoDocumentosVencidosQuery(SextaAs8), default);
        Assert.All(previa.Destinatarios, d => Assert.True(d.RecebeSininho));
    }
}
