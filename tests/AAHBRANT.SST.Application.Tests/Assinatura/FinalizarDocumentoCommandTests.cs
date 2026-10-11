using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// A finalização é o que transforma o documento em prova: fecha para novas assinaturas, congela o hash e
// guarda o comprovante. Até 11/10 ela não tinha teste (auditoria de 06/10) e tinha três falhas cobertas aqui.
public class FinalizarDocumentoCommandTests
{
    private sealed class QrCodeFalso : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) => new(Array.Empty<byte>(), $"https://validar.teste/#/validar/{token}");
    }

    private sealed class PdfFalso : IDocumentoAssinaturaPdfService
    {
        public DocumentoAssinaturaPdfModelo? Modelo { get; private set; }
        public byte[] Gerar(DocumentoAssinaturaPdfModelo modelo)
        {
            Modelo = modelo;
            return new byte[] { 0x25, 0x50, 0x44, 0x46, (byte)modelo.Signatarios.Count };
        }
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, DocumentoAssinatura Documento, Trabalhador Trabalhador)> PrepararAsync(bool trabalhadorAtivo = true)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador { ObraId = obra.Id, Nome = "Fulano", Cpf = "12345678901", DataAdmissao = DateTime.UtcNow, Ativo = trabalhadorAtivo, Funcao = new Funcao { Nome = "Pedreiro" } };
        db.Trabalhadores.Add(trabalhador);
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = Guid.NewGuid() };
        db.DocumentosAssinatura.Add(documento);
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id, TrabalhadorId = trabalhador.Id,
            MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (db, documento, trabalhador);
    }

    private static FinalizarDocumentoCommandHandler Handler(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db, PdfFalso pdf) =>
        new(db, new AuditoriaFalsa(), new MediatorSimulado(_ => null), pdf, new QrCodeFalso());

    // Um PDF exportado antes da última assinatura não pode virar a "prova congelada".
    [Fact]
    public async Task Finalizar_ComPdfAntigo_GeraComprovanteNovoEGravaOHashDoArquivo()
    {
        var (db, documento, _) = await PrepararAsync();
        documento.PdfConteudo = new byte[] { 9, 9, 9 };
        await db.SaveChangesAsync();
        var pdf = new PdfFalso();

        await Handler(db, pdf).Handle(new FinalizarDocumentoCommand(documento.Id), default);

        var salvo = db.DocumentosAssinatura.Single(d => d.Id == documento.Id);
        Assert.Equal(StatusDocumentoAssinatura.Finalizado, salvo.Status);
        Assert.NotEqual(new byte[] { 9, 9, 9 }, salvo.PdfConteudo);
        Assert.Equal(HashArquivoCalculador.Calcular(salvo.PdfConteudo!), salvo.HashPdf);
        Assert.NotNull(salvo.ConteudoHash);
    }

    // O QR já impresso nos PDFs exportados antes precisa continuar válido depois de finalizar.
    [Fact]
    public async Task Finalizar_DocumentoJaRastreado_MantemOTokenDeValidacao()
    {
        var (db, documento, _) = await PrepararAsync();
        documento.TokenValidacaoPublica = "token-ja-impresso";
        await db.SaveChangesAsync();

        await Handler(db, new PdfFalso()).Handle(new FinalizarDocumentoCommand(documento.Id), default);

        Assert.Equal("token-ja-impresso", db.DocumentosAssinatura.Single(d => d.Id == documento.Id).TokenValidacaoPublica);
    }

    [Fact]
    public async Task Finalizar_SignatarioDesligado_ContinuaNoComprovante()
    {
        var (db, documento, trabalhador) = await PrepararAsync(trabalhadorAtivo: false);
        var pdf = new PdfFalso();

        var resultado = await Handler(db, pdf).Handle(new FinalizarDocumentoCommand(documento.Id), default);

        Assert.Single(resultado.Signatarios);
        Assert.Equal(trabalhador.Nome, Assert.Single(pdf.Modelo!.Signatarios).TrabalhadorNome);
    }

    [Fact]
    public async Task FinalizacaoAutomatica_DocumentoComAssinatura_PedeAFinalizacao()
    {
        var (db, documento, _) = await PrepararAsync();
        var mediator = new MediatorSimulado(_ => null);

        await FinalizacaoAutomaticaDocumento.TentarAsync(db, mediator, NullLogger.Instance, documento.EntidadeTipo, documento.EntidadeId, default);

        var pedido = Assert.IsType<FinalizarDocumentoCommand>(Assert.Single(mediator.Requisicoes));
        Assert.Equal(documento.Id, pedido.DocumentoAssinaturaId);
    }

    [Fact]
    public async Task FinalizacaoAutomatica_SemAssinaturas_NaoFinaliza()
    {
        using var db = DbContextFactory.Criar();
        var documento = new DocumentoAssinatura { EntidadeTipo = "Dds", EntidadeId = Guid.NewGuid() };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var mediator = new MediatorSimulado(_ => null);

        await FinalizacaoAutomaticaDocumento.TentarAsync(db, mediator, NullLogger.Instance, "Dds", documento.EntidadeId, default);

        Assert.Empty(mediator.Requisicoes);
    }

    // Falha ao finalizar não pode derrubar o fluxo que chamou (encerrar DDS, assinar inspeção...).
    [Fact]
    public async Task FinalizacaoAutomatica_FalhaNaFinalizacao_NaoPropagaOErro()
    {
        var (db, documento, _) = await PrepararAsync();
        var mediator = new MediatorSimulado(_ => throw new InvalidOperationException("falhou"));

        await FinalizacaoAutomaticaDocumento.TentarAsync(db, mediator, NullLogger.Instance, documento.Id, default);
    }

    // Signatário desligado depois da finalização não pode fazer o documento parecer adulterado.
    [Fact]
    public async Task VerificarIntegridade_SignatarioDesligadoDepois_ContinuaIntegro()
    {
        var (db, documento, trabalhador) = await PrepararAsync();
        await Handler(db, new PdfFalso()).Handle(new FinalizarDocumentoCommand(documento.Id), default);
        trabalhador.Ativo = false;
        await db.SaveChangesAsync();

        var verificacao = await new AAHBRANT.SST.Application.Assinatura.Queries.VerificarIntegridadeQueryHandler(db)
            .Handle(new AAHBRANT.SST.Application.Assinatura.Queries.VerificarIntegridadeQuery(documento.Id), default);

        Assert.True(verificacao.Integro);
    }

    [Fact]
    public async Task Registrar_GuardaAConfiancaDaIdentificacao()
    {
        var (db, _, trabalhador) = await PrepararAsync();
        var documento = new DocumentoAssinatura { EntidadeTipo = "Apr", EntidadeId = Guid.NewGuid() };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        await new RegistradorAssinaturaService(db, new AuditoriaFalsa()).RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial, Confianca: 91.237), null, default);

        Assert.Equal(91.24, db.DocumentoSignatarios.Single(s => s.DocumentoAssinaturaId == documento.Id).ConfiancaAutenticacao);
    }
}
