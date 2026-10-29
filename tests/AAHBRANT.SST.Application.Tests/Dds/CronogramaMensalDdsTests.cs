using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Cronogramas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class CronogramaMensalDdsTests
{
    private static async Task<(SstDbContext Db, Obra Obra, CatalogoTemaDds Tema)> Preparar()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra A", Codigo = "A", LogoConteudo = [1, 2, 3] };
        var tema = new CatalogoTemaDds { Nome = "Proteção das mãos", Descricao = "Tema original" };
        db.Obras.Add(obra);
        db.CatalogosTemaDds.Add(tema);
        db.Usuarios.Add(new Usuario { Nome = "Responsável", Email = "teste@example.test", AzureAdObjectId = "autor" });
        await db.SaveChangesAsync();
        return (db, obra, tema);
    }

    private static Task<CronogramaMensalDdsDto> Salvar(SstDbContext db, Guid obra, int versao, params DiaCronogramaDdsEntrada[] dias) =>
        new SalvarCronogramaMensalDdsCommandHandler(db, new Numeros()).Handle(new(obra, 2026, 10, versao, dias.ToList(), "autor"), default);
    private static DiaCronogramaDdsEntrada Dia(int dia, params Guid[] temas) => new(new(2026, 10, dia), temas.ToList());
    private static EmissorCronogramaMensalDds Emissor(SstDbContext db, Pdf? pdf = null) =>
        new(db, pdf ?? new Pdf(), new RegistradorRastreabilidadeService(db, new Qr()));

    [Fact]
    public async Task PlanejamentoPersisteSemCriarDdsNemAlertas()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        var outro = new CatalogoTemaDds { Nome = "Organização" };
        db.CatalogosTemaDds.Add(outro); await db.SaveChangesAsync();
        var salvo = await Salvar(db, obra.Id, 0, Dia(1, tema.Id, outro.Id), Dia(2, tema.Id));
        db.ChangeTracker.Clear();
        var lido = await new ObterCronogramaMensalDdsQueryHandler(db).Handle(new(obra.Id, 2026, 10), default);
        Assert.Equal(salvo.Id, lido.Id);
        Assert.Equal(2, lido.Dias.Count);
        Assert.Equal(2, lido.Dias[0].Temas.Count);
        Assert.Empty(await db.Dds.ToListAsync());
        Assert.Empty(await db.DdsSemanais.ToListAsync());
        Assert.Empty(await db.Alertas.ToListAsync());
        Assert.Empty(await db.DocumentosAssinatura.ToListAsync());
    }

    [Fact]
    public async Task VersaoAntigaNaoSobrescreveEdicaoDeOutraPessoa()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        var salvo = await Salvar(db, obra.Id, 0, Dia(1, tema.Id));
        await Salvar(db, obra.Id, salvo.Versao, Dia(2, tema.Id));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Salvar(db, obra.Id, salvo.Versao, Dia(3, tema.Id)));
        var atual = await new ObterCronogramaMensalDdsQueryHandler(db).Handle(new(obra.Id, 2026, 10), default);
        Assert.Equal(new DateOnly(2026, 10, 2), Assert.Single(atual.Dias).Data);
    }

    [Fact]
    public async Task TemaEditadoOuDesativadoPreservaPlanejamentoAnterior()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        var salvo = await Salvar(db, obra.Id, 0, Dia(1, tema.Id));
        tema.Nome = "Título alterado"; tema.Ativo = false; await db.SaveChangesAsync();
        var mantido = await Salvar(db, obra.Id, salvo.Versao, Dia(1, tema.Id));
        Assert.Equal("Proteção das mãos", mantido.Dias[0].Temas[0].Nome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Salvar(db, obra.Id, mantido.Versao, Dia(1, tema.Id), Dia(2, tema.Id)));
        var removido = await Salvar(db, obra.Id, mantido.Versao);
        Assert.Empty(removido.Dias);
    }

    [Fact]
    public async Task RejeitaDiaForaDoMesDuplicadoETemaInexistente()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Salvar(db, obra.Id, 0, new DiaCronogramaDdsEntrada(new(2026, 11, 1), [tema.Id])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Salvar(db, obra.Id, 0, Dia(1, tema.Id), Dia(1, tema.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Salvar(db, obra.Id, 0, Dia(1, tema.Id, tema.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Salvar(db, obra.Id, 0, Dia(1, Guid.NewGuid())));
        Assert.Empty(await db.CronogramasMensaisDds.ToListAsync());
    }

    [Fact]
    public async Task EmissaoIdempotenteCongelaLogoETemasERevisaoSeguintePreservaAnterior()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        var salvo = await Salvar(db, obra.Id, 0, Dia(1, tema.Id));
        var emissor = Emissor(db);
        var primeiro = await emissor.GerarAsync(salvo.Id!.Value, salvo.Versao, false, true, default);
        var repetido = await emissor.GerarAsync(salvo.Id.Value, salvo.Versao, false, true, default);
        Assert.Equal(primeiro.Conteudo, repetido.Conteudo);
        Assert.Single(await db.CronogramasMensaisDdsRevisoes.ToListAsync());
        var original = await db.DocumentosAssinatura.SingleAsync();
        Assert.Equal(StatusDocumentoAssinatura.Finalizado, original.Status);
        Assert.Empty(original.Signatarios);
        Assert.NotNull(original.HashPdf);
        obra.LogoConteudo = [4, 5, 6]; await db.SaveChangesAsync();
        var alterado = await Salvar(db, obra.Id, salvo.Versao, Dia(2, tema.Id));
        var segundo = await emissor.GerarAsync(salvo.Id.Value, alterado.Versao, false, true, default);
        Assert.NotEqual(primeiro.Conteudo, segundo.Conteudo);
        Assert.Equal(primeiro.Conteudo, original.PdfConteudo);
        var revisoes = await db.CronogramasMensaisDdsRevisoes.OrderBy(r => r.Numero).ToListAsync();
        Assert.Equal(2, revisoes.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, JsonSerializer.Deserialize<CronogramaDdsPdfModelo>(revisoes[0].ConteudoJson)!.Logo);
    }

    [Fact]
    public async Task PreviaNaoEmiteRevisaoENaoOmiteFimDeSemana()
    {
        var (db, obra, tema) = await Preparar();
        await using var dispose = db;
        var salvo = await Salvar(db, obra.Id, 0, Dia(3, tema.Id));
        var pdf = new Pdf();
        await Emissor(db, pdf).GerarAsync(salvo.Id!.Value, salvo.Versao, false, false, default);
        Assert.True(pdf.Ultimo!.SemanaCompleta);
        Assert.Empty(await db.CronogramasMensaisDdsRevisoes.ToListAsync());
        Assert.Empty(await db.DocumentosAssinatura.ToListAsync());
        obra.LogoConteudo = null; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Emissor(db).GerarAsync(salvo.Id.Value, salvo.Versao, false, true, default));
    }

    [Fact]
    public async Task ObraForaDoEscopoNaoPodeSerLidaEditadaEmitidaOuTerRevisaoBaixada()
    {
        ChavesCpfDeTeste.Configurar();
        var escopo = new CurrentUserService();
        await using var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, escopo);
        var obra = new Obra { Codigo = "A", Nome = "Restrita", LogoConteudo = [1] };
        var usuario = new Usuario { Nome = "Responsável", Email = "teste@example.test", AzureAdObjectId = "autor" };
        db.AddRange(obra, usuario); await db.SaveChangesAsync();
        var salvo = await Salvar(db, obra.Id, 0);
        await Emissor(db).GerarAsync(salvo.Id!.Value, salvo.Versao, false, true, default);
        var revisao = await db.CronogramasMensaisDdsRevisoes.SingleAsync();
        db.ChangeTracker.Clear(); escopo.DefinirEscopo(false, [Guid.NewGuid()]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ObterCronogramaMensalDdsQueryHandler(db).Handle(new(obra.Id, 2026, 10), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Salvar(db, obra.Id, 1));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Emissor(db).GerarAsync(salvo.Id.Value, 1, false, true, default));
        Assert.Empty(await db.CronogramasMensaisDdsRevisoes.ToListAsync());
        Assert.False(await EscopoDocumentoAssinatura.PodeVerAsync(db, nameof(CronogramaMensalDdsRevisao), revisao.Id, default));
    }

    private class Numeros : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult(prefixo + "-2026-0001");
    }
    private class Qr : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) => new([1], "https://example.test/validar/" + token);
    }
    private class Pdf : ICronogramaMensalDdsPdfService
    {
        public CronogramaDdsPdfModelo? Ultimo { get; private set; }
        public byte[] Gerar(CronogramaDdsPdfModelo modelo, int? revisao = null, RastreabilidadeDocumentoResultado? rastreio = null)
        {
            Ultimo = modelo;
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(modelo));
        }
    }
}
