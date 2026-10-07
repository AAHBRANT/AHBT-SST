using AAHBRANT.SST.Application.Relatorios;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Relatorios;

public class PublicadorRelatorioTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46 };

    private static PublicadorRelatorioService Criar(SstDbContext db, TelegramSimulado telegram, SininhoSimulado sininho) =>
        new(db, telegram, sininho, NullLogger<PublicadorRelatorioService>.Instance);

    private static NovoRelatorio Novo(Guid? obraId, string chave = "dds:1", bool comPdf = true, TipoRelatorio tipo = TipoRelatorio.ListaPresencaDds) =>
        new(tipo, obraId, chave, "Presença do DDS · Obra Sul · 07/10", "Presença do DDS, Obra Sul: 28 de 30 presentes.",
            Png, comPdf ? Pdf : null, comPdf ? "Lista-de-Presenca.pdf" : null, Guid.NewGuid(), new DateTime(2026, 10, 7), new DateTime(2026, 10, 7));

    private static async Task<Usuario> NovoUsuario(SstDbContext db, string nome, StatusUsuario status = StatusUsuario.Ativo)
    {
        var u = new Usuario { Nome = nome, Email = $"{nome.ToLowerInvariant()}@example.test", Status = status };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    [Fact]
    public async Task Publica_GuardaORelatorio_EnviaImagemEPdfNoTelegram()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        db.Obras.Add(obra);
        await db.SaveChangesAsync();
        var telegram = new TelegramSimulado();

        var id = await Criar(db, telegram, new SininhoSimulado()).PublicarAsync(Novo(obra.Id));

        Assert.NotNull(id);
        var guardado = await db.RelatoriosGerados.SingleAsync();
        Assert.Equal(Png, guardado.Imagem);
        Assert.Equal(Pdf, guardado.Pdf);
        Assert.Equal("Lista-de-Presenca.pdf", guardado.PdfNome);
        Assert.Equal(("Presença do DDS, Obra Sul: 28 de 30 presentes."), Assert.Single(telegram.Imagens).Legenda);
        Assert.Equal("Lista-de-Presenca.pdf", Assert.Single(telegram.Documentos).Nome);
        Assert.Empty(telegram.Textos);
        var envio = await db.RelatorioEnvios.SingleAsync();
        Assert.True(envio.Sucesso);
        Assert.Equal(CanalEnvioRelatorio.Telegram, envio.Canal);
    }

    [Fact]
    public async Task OMesmoRelatorio_NaoSaiDuasVezes()
    {
        var db = DbContextFactory.Criar();
        var telegram = new TelegramSimulado();
        var servico = Criar(db, telegram, new SininhoSimulado());
        var obraId = Guid.NewGuid();

        var primeiro = await servico.PublicarAsync(Novo(obraId));
        var segundo = await servico.PublicarAsync(Novo(obraId));

        Assert.NotNull(primeiro);
        Assert.Null(segundo);
        Assert.Equal(1, await db.RelatoriosGerados.CountAsync());
        Assert.Single(telegram.Imagens);
    }

    [Fact]
    public async Task SeATelegramRecusaAImagem_EnviaOResumoEmTexto()
    {
        var db = DbContextFactory.Criar();
        var telegram = new TelegramSimulado { ImagemFunciona = false };

        await Criar(db, telegram, new SininhoSimulado()).PublicarAsync(Novo(Guid.NewGuid(), comPdf: false));

        Assert.Empty(telegram.Imagens);
        Assert.Equal("Presença do DDS, Obra Sul: 28 de 30 presentes.", Assert.Single(telegram.Textos));
        Assert.Contains("texto", (await db.RelatorioEnvios.SingleAsync()).Erro);
    }

    [Fact]
    public async Task SeOTelegramCair_RegistraAFalhaENaoDerrubaOProcesso()
    {
        var db = DbContextFactory.Criar();

        var id = await Criar(db, new TelegramSimulado { LancarExcecao = true }, new SininhoSimulado()).PublicarAsync(Novo(Guid.NewGuid()));

        Assert.NotNull(id); // o relatório foi guardado mesmo assim
        var envio = await db.RelatorioEnvios.SingleAsync();
        Assert.False(envio.Sucesso);
        Assert.Contains("fora do ar", envio.Erro);
    }

    [Fact]
    public async Task Sininho_VaiSoParaQuemEstaCadastradoNaObraComOTipoMarcado()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var outraObra = new Obra { Nome = "Obra Norte", Codigo = "N" };
        db.AddRange(obra, outraObra);
        var daObra = await NovoUsuario(db, "Daobra");
        var global = await NovoUsuario(db, "Global");
        var deOutraObra = await NovoUsuario(db, "Outra");
        var semOTipo = await NovoUsuario(db, "SemTipo");
        var inativo = await NovoUsuario(db, "Inativo", StatusUsuario.Inativo);
        db.DestinatariosRelatorio.AddRange(
            new DestinatarioRelatorio { UsuarioId = daObra.Id, ObraId = obra.Id, ListaPresenca = true },
            new DestinatarioRelatorio { UsuarioId = global.Id, ObraId = null, ListaPresenca = true },
            new DestinatarioRelatorio { UsuarioId = deOutraObra.Id, ObraId = outraObra.Id, ListaPresenca = true },
            new DestinatarioRelatorio { UsuarioId = semOTipo.Id, ObraId = obra.Id, BoletimSemanal = true },
            new DestinatarioRelatorio { UsuarioId = inativo.Id, ObraId = obra.Id, ListaPresenca = true });
        await db.SaveChangesAsync();
        var sininho = new SininhoSimulado();

        await Criar(db, new TelegramSimulado(), sininho).PublicarAsync(Novo(obra.Id));

        Assert.Equivalent(new[] { daObra.Id, global.Id }, sininho.Enviadas.Select(e => e.UsuarioId));
        Assert.All(sininho.Enviadas, e => Assert.StartsWith("Presença do DDS", e.Titulo));
    }

    [Fact]
    public async Task RelatorioConsolidado_VaiSoParaQuemRecebeTodasAsObras()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        db.Obras.Add(obra);
        var diretoria = await NovoUsuario(db, "Diretoria");
        var tecnico = await NovoUsuario(db, "Tecnico");
        db.DestinatariosRelatorio.AddRange(
            new DestinatarioRelatorio { UsuarioId = diretoria.Id, ObraId = null, BoletimSemanal = true },
            new DestinatarioRelatorio { UsuarioId = tecnico.Id, ObraId = obra.Id, BoletimSemanal = true });
        await db.SaveChangesAsync();
        var sininho = new SininhoSimulado();

        await Criar(db, new TelegramSimulado(), sininho).PublicarAsync(Novo(null, "boletim:consolidado:20261005", tipo: TipoRelatorio.BoletimSemanal));

        Assert.Equal(diretoria.Id, Assert.Single(sininho.Enviadas).UsuarioId);
    }

    [Fact]
    public async Task FalhaNoSininhoDeUmUsuario_NaoImpedeOsOutros_EFicaRegistrada()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        db.Obras.Add(obra);
        var semTeams = await NovoUsuario(db, "SemTeams");
        var comTeams = await NovoUsuario(db, "ComTeams");
        db.DestinatariosRelatorio.AddRange(
            new DestinatarioRelatorio { UsuarioId = semTeams.Id, ObraId = obra.Id, ListaPresenca = true },
            new DestinatarioRelatorio { UsuarioId = comTeams.Id, ObraId = obra.Id, ListaPresenca = true });
        await db.SaveChangesAsync();
        var sininho = new SininhoSimulado();
        sininho.FalhaPara.Add(semTeams.Id);

        await Criar(db, new TelegramSimulado(), sininho).PublicarAsync(Novo(obra.Id));

        Assert.Equal(comTeams.Id, Assert.Single(sininho.Enviadas).UsuarioId);
        var envios = await db.RelatorioEnvios.Where(e => e.Canal == CanalEnvioRelatorio.Sininho).ToListAsync();
        Assert.True(envios.Single(e => e.UsuarioId == comTeams.Id).Sucesso);
        var falha = envios.Single(e => e.UsuarioId == semTeams.Id);
        Assert.False(falha.Sucesso);
        Assert.Contains("AzureAdObjectId", falha.Erro);
    }
}
