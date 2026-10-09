using AAHBRANT.SST.Application.Aptidoes.Commands;
using AAHBRANT.SST.Application.Asos.Commands;
using AAHBRANT.SST.Application.Asos.Queries;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.ExamesComplementares.Commands;
using AAHBRANT.SST.Application.ExamesComplementares.Queries;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Common;

// Auditoria de 09/10/2026 (antes de levar o SST às demais obras): ASO, exame complementar, aptidão,
// termo manual de EPI, certificado anexado e documentos do Motor de Assinatura não têm filtro global
// por obra, e nenhum handler conferia a obra. Usuário restrito à obra A não pode ler, alterar nem
// excluir registro da obra B — e continua vendo o histórico de trabalhador DESLIGADO da própria obra.
public class EscopoObraSaudeAssinaturaTests
{
    static EscopoObraSaudeAssinaturaTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private sealed record Cenario(
        SstDbContext Global, SstDbContext Restrito,
        Trabalhador DeA, Trabalhador DeB,
        Aso AsoA, Aso AsoB,
        ExameComplementar ExameB,
        Treinamento TreinamentoB,
        DocumentoAssinatura DocA, DocumentoAssinatura DocB,
        DocumentoSignatario SignatarioB);

    private static async Task<Cenario> CriarAsync()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());
        var usuarioRestrito = new CurrentUserService();
        usuarioRestrito.DefinirEscopo(false, new[] { ObraA });
        var restrito = new SstDbContext(opcoes, usuarioRestrito);

        var funcao = new Funcao { Nome = "Pedreiro" };
        var deA = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Trabalhador A", Cpf = "00000000001" };
        var deB = new Trabalhador { ObraId = ObraB, Funcao = funcao, Nome = "Trabalhador B", Cpf = "00000000002" };
        global.Funcoes.Add(funcao);
        global.Trabalhadores.AddRange(deA, deB);
        await global.SaveChangesAsync();

        var hoje = DateTime.UtcNow;
        var asoA = new Aso { TrabalhadorId = deA.Id, DataExame = hoje, DataValidade = hoje.AddYears(1), ObservacoesClinicas = "clinico A" };
        var asoB = new Aso { TrabalhadorId = deB.Id, DataExame = hoje, DataValidade = hoje.AddYears(1), ObservacoesClinicas = "clinico B" };
        var exameB = new ExameComplementar { TrabalhadorId = deB.Id, DataRealizacao = hoje, DataValidade = hoje.AddYears(1), Resultado = "Normal" };
        var treinamentoB = new Treinamento { TrabalhadorId = deB.Id, CursoTreinamentoId = Guid.NewGuid(), DataRealizacao = hoje, DataValidade = hoje.AddYears(1) };
        global.Asos.AddRange(asoA, asoB);
        global.ExamesComplementares.Add(exameB);
        global.Treinamentos.Add(treinamentoB);
        global.ArquivosCertificadoTreinamento.Add(new ArquivoCertificadoTreinamento
        {
            Treinamento = treinamentoB, NomeArquivo = "cert.pdf", ContentType = "application/pdf", Conteudo = new byte[] { 1 },
        });
        global.TermosCompromissoEpiManual.Add(new TermoCompromissoEpiManual
        {
            TrabalhadorId = deB.Id, DataAssinaturaPapel = hoje, RegistradoPorUsuarioId = Guid.NewGuid(),
            ArquivoNome = "termo.pdf", ArquivoContentType = "application/pdf", ArquivoConteudo = new byte[] { 1 },
        });

        var ddsA = new Domain.Entidades.Dds { ObraId = ObraA };
        var ddsB = new Domain.Entidades.Dds { ObraId = ObraB };
        global.Dds.AddRange(ddsA, ddsB);
        await global.SaveChangesAsync();

        var docA = new DocumentoAssinatura { EntidadeTipo = "Dds", EntidadeId = ddsA.Id, PdfConteudo = new byte[] { 1 } };
        var docB = new DocumentoAssinatura { EntidadeTipo = "Dds", EntidadeId = ddsB.Id, PdfConteudo = new byte[] { 2 } };
        var docFichaB = new DocumentoAssinatura { EntidadeTipo = "FichaEpiTrabalhador", EntidadeId = deB.Id };
        var docTipoNovo = new DocumentoAssinatura { EntidadeTipo = "TipoAindaNaoMapeado", EntidadeId = Guid.NewGuid() };
        var signatarioB = new DocumentoSignatario
        {
            DocumentoAssinatura = docB, TrabalhadorId = deB.Id, AssinadoEm = hoje,
            FotoEvidenciaConteudo = new byte[] { 9 }, FotoEvidenciaContentType = "image/jpeg",
        };
        global.DocumentosAssinatura.AddRange(docA, docB, docFichaB, docTipoNovo);
        global.DocumentoSignatarios.Add(signatarioB);
        await global.SaveChangesAsync();

        return new Cenario(global, restrito, deA, deB, asoA, asoB, exameB, treinamentoB, docA, docB, signatarioB);
    }

    [Fact]
    public async Task Aso_Listar_RestritoVeSoAPropriaObra()
    {
        var c = await CriarAsync();

        var lista = await new ListarAsosQueryHandler(c.Restrito).Handle(new ListarAsosQuery(), default);

        Assert.Equal(new[] { c.AsoA.Id }, lista.Select(a => a.Id));
    }

    [Fact]
    public async Task Aso_Listar_GlobalVeTodos()
    {
        var c = await CriarAsync();

        var lista = await new ListarAsosQueryHandler(c.Global).Handle(new ListarAsosQuery(), default);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task Aso_Listar_TrabalhadorDesligadoDaPropriaObra_ContinuaVisivel()
    {
        var c = await CriarAsync();
        var deA = await c.Global.Trabalhadores.FirstAsync(t => t.Id == c.DeA.Id);
        deA.Ativo = false;
        await c.Global.SaveChangesAsync();

        var lista = await new ListarAsosQueryHandler(c.Restrito).Handle(new ListarAsosQuery(), default);

        Assert.Equal(new[] { c.AsoA.Id }, lista.Select(a => a.Id));
    }

    [Fact]
    public async Task Aso_ObterPorIdDeOutraObra_RetornaNulo()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterAsoPorIdQueryHandler(c.Restrito).Handle(new ObterAsoPorIdQuery(c.AsoB.Id), default));
        Assert.NotNull(await new ObterAsoPorIdQueryHandler(c.Restrito).Handle(new ObterAsoPorIdQuery(c.AsoA.Id), default));
    }

    [Fact]
    public async Task Aso_EditarEExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();
        var hoje = DateTime.UtcNow;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AtualizarAsoCommandHandler(c.Restrito).Handle(
            new AtualizarAsoCommand(c.AsoB.Id, c.DeB.Id, TipoExameAso.Periodico, hoje, hoje.AddYears(1), ResultadoAso.Inapto, null, null, null), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirAsoCommandHandler(c.Restrito).Handle(new ExcluirAsoCommand(c.AsoB.Id), default));

        Assert.True(await c.Global.Asos.AnyAsync(a => a.Id == c.AsoB.Id));
    }

    [Fact]
    public async Task ExameComplementar_ListarEExcluirDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        var lista = await new ListarExamesComplementaresQueryHandler(c.Restrito).Handle(new ListarExamesComplementaresQuery(), default);
        Assert.Empty(lista);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ExcluirExameComplementarCommandHandler(c.Restrito).Handle(new ExcluirExameComplementarCommand(c.ExameB.Id), default));
    }

    [Fact]
    public async Task Aptidao_CriarParaTrabalhadorDeOutraObra_Bloqueia()
    {
        var c = await CriarAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CriarAptidaoCommandHandler(c.Restrito).Handle(
            new CriarAptidaoCommand(c.DeB.Id, "Trabalho em altura", ResultadoAso.Apto, DateTime.UtcNow, null, null, null), default));
    }

    [Fact]
    public async Task ArquivosDeOutraObra_TermoEpiECertificado_RetornamNulo()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterArquivoTermoManualEpiQueryHandler(c.Restrito).Handle(new ObterArquivoTermoManualEpiQuery(c.DeB.Id), default));
        Assert.Null(await new ObterArquivoCertificadoTreinamentoQueryHandler(c.Restrito).Handle(new ObterArquivoCertificadoTreinamentoQuery(c.TreinamentoB.Id), default));
        Assert.NotNull(await new ObterArquivoTermoManualEpiQueryHandler(c.Global).Handle(new ObterArquivoTermoManualEpiQuery(c.DeB.Id), default));
    }

    [Fact]
    public async Task Assinatura_Listar_RestritoVeSoDocumentosDaPropriaObra()
    {
        var c = await CriarAsync();

        var restrito = await new ListarDocumentosAssinaturaQueryHandler(c.Restrito).Handle(new ListarDocumentosAssinaturaQuery(), default);
        var global = await new ListarDocumentosAssinaturaQueryHandler(c.Global).Handle(new ListarDocumentosAssinaturaQuery(), default);

        // Ficha da obra B e tipo ainda não mapeado ficam de fora para o restrito (falha fechada).
        Assert.Equal(new[] { c.DocA.Id }, restrito.Select(d => d.Id));
        Assert.Equal(4, global.Count);
    }

    [Fact]
    public async Task Assinatura_PdfEFotoDeOutraObra_RetornamNulo()
    {
        var c = await CriarAsync();

        Assert.Null(await new ObterPdfDocumentoQueryHandler(c.Restrito).Handle(new ObterPdfDocumentoQuery(c.DocB.Id), default));
        Assert.NotNull(await new ObterPdfDocumentoQueryHandler(c.Restrito).Handle(new ObterPdfDocumentoQuery(c.DocA.Id), default));
        Assert.Null(await new ObterFotoEvidenciaAssinaturaQueryHandler(c.Restrito).Handle(new ObterFotoEvidenciaAssinaturaQuery(c.SignatarioB.Id), default));
        Assert.NotNull(await new ObterFotoEvidenciaAssinaturaQueryHandler(c.Global).Handle(new ObterFotoEvidenciaAssinaturaQuery(c.SignatarioB.Id), default));
    }
}
