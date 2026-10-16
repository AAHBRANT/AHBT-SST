using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class AvisoDocumentosVencidosTests
{
    // Sexta-feira, 16/10/2026.
    private static readonly DateTime SextaAs7 = new(2026, 10, 16, 7, 0, 0);

    private sealed class EmailFalso : IEmailService
    {
        public List<(string Para, string Assunto, string Html)> Enviados { get; } = new();
        public Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
        {
            Enviados.Add((para, assunto, corpoHtml));
            return Task.CompletedTask;
        }
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
        public EnviarAvisoDocumentosVencidosCommandHandler Handler() =>
            new(Db, Email, Fila, NullLogger<EnviarAvisoDocumentosVencidosCommandHandler>.Instance);
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

        var enviados = await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7), default);

        Assert.Equal(3, enviados);
        Assert.Equal(new[] { "eng@x.com", "gestora@x.com", "teca@x.com" }, c.Email.Enviados.Select(e => e.Para).OrderBy(e => e));
        var (_, assunto, html) = c.Email.Enviados[0];
        Assert.Contains("PGR vencido", assunto);
        Assert.Contains("há 6 dia(s)", html);
        Assert.Contains("cid:logo-aahbrant", html);
        Assert.Contains("text-align:justify", html);
    }

    [Fact]
    public async Task SabadoOuAntesDas7_NaoEnvia()
    {
        var c = await MontarAsync(nameof(SabadoOuAntesDas7_NaoEnvia));

        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 17, 9, 0, 0)), default));
        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 16, 6, 59, 0)), default));
        Assert.Empty(c.Email.Enviados);
    }

    [Fact]
    public async Task SegundaChamadaNoMesmoDia_NaoReenvia_NoDiaSeguinteEnviaDeNovo()
    {
        var c = await MontarAsync(nameof(SegundaChamadaNoMesmoDia_NaoReenvia_NoDiaSeguinteEnviaDeNovo));

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7), default);
        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7.AddHours(3)), default);
        Assert.Equal(3, c.Email.Enviados.Count);

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(new DateTime(2026, 10, 19, 7, 5, 0)), default);
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

        Assert.Equal(0, await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7), default));
    }

    [Fact]
    public async Task PcmsoVencido_EntraNoMesmoEmailDoPgr_UmEmailPorPessoa()
    {
        var c = await MontarAsync(nameof(PcmsoVencido_EntraNoMesmoEmailDoPgr_UmEmailPorPessoa));
        c.Db.PcmsoDetalhes.Add(new PcmsoDetalhe
        {
            Nome = "PCMSO Parque Roger", ObraId = c.ObraA.Id, Status = StatusPcmsoDocumento.Vigente,
            Validade = new DateTime(2026, 9, 30), DataEmissao = new DateTime(2025, 9, 30),
        });
        await c.Db.SaveChangesAsync();

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7), default);

        var tecnico = Assert.Single(c.Email.Enviados, e => e.Para == "teca@x.com");
        Assert.Contains("2 documentos vencidos", tecnico.Assunto);
        Assert.Contains("PCMSO Parque Roger", tecnico.Html);
        Assert.Contains("PGR Parque Roger", tecnico.Html);
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

        await c.Handler().Handle(new EnviarAvisoDocumentosVencidosCommand(SextaAs7), default);

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
}
