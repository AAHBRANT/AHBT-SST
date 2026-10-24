using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.LeituraIa;

// Importação da pasta da terceirizada (23/10/2026): o handler casa o nome escolhido pela IA com o
// cadastro (documento exigido / curso), limpa CPF e CNPJ e traduz tipo e resultado do ASO.
public class ClassificarDocumentoTerceirizadoQueryTests
{
    private sealed class ExtratorFalso(params string[] paginas) : IExtratorTextoPdf
    {
        public IReadOnlyList<string> ExtrairPaginas(byte[] pdf) => paginas;
    }

    private sealed class ClassificadorFalso(DocumentoTerceirizadoLidoIa resposta) : IClassificadorDocumentoTerceirizadoIa
    {
        public IReadOnlyList<string> DocumentosRecebidos { get; private set; } = Array.Empty<string>();
        public byte[]? ImagemRecebida { get; private set; }
        public int Chamadas { get; private set; }

        public Task<DocumentoTerceirizadoLidoIa> ClassificarAsync(string nomeArquivo, IReadOnlyList<string> paginas, byte[]? imagem,
            IReadOnlyList<string> documentosEmpresa, IReadOnlyList<string> cursos, CancellationToken ct)
        {
            Chamadas++;
            DocumentosRecebidos = documentosEmpresa;
            ImagemRecebida = imagem;
            return Task.FromResult(resposta);
        }
    }

    private static SstDbContext CriarDb(string nome) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, new CurrentUserService());

    private static DocumentoTerceirizadoLidoIa Lido(string tipo, string? documento = null, string? curso = null,
        string? asoTipo = "nenhum", string? asoResultado = "nenhum") => new(
        tipo, documento, curso, "João da Silva", "529.982.247-25", "Pedreiro", "XPTO Ltda", "12.345.678/0001-99",
        new DateTime(2026, 2, 10), new DateTime(2027, 2, 10), asoTipo, asoResultado, null, null, 8, null, "resumo");

    [Fact]
    public async Task DocumentoDaEmpresa_CasaComDocumentoExigidoPeloNome()
    {
        var db = CriarDb(nameof(DocumentoDaEmpresa_CasaComDocumentoExigidoPeloNome));
        var pgr = new DocumentoExigidoEmpresa { Nome = "PGR", Ordem = 1 };
        db.DocumentosExigidosEmpresa.AddRange(pgr, new DocumentoExigidoEmpresa { Nome = "PCMSO", Ordem = 2 });
        await db.SaveChangesAsync();
        var classificador = new ClassificadorFalso(Lido("documento_empresa", documento: " pgr "));

        var r = await new ClassificarDocumentoTerceirizadoQueryHandler(new ExtratorFalso(new string('x', 60)), classificador, db)
            .Handle(new ClassificarDocumentoTerceirizadoQuery("pgr.pdf", "application/pdf", new byte[] { 1 }), default);

        Assert.Equal("documento_empresa", r.Tipo);
        Assert.Equal(pgr.Id, r.DocumentoExigidoId);
        Assert.Equal(new[] { "PGR", "PCMSO" }, classificador.DocumentosRecebidos);
        Assert.Equal("12345678000199", r.EmpresaCnpj);
        Assert.Null(r.AsoTipo);
    }

    [Fact]
    public async Task Aso_TraduzTipoEResultadoELimpaCpf()
    {
        var db = CriarDb(nameof(Aso_TraduzTipoEResultadoELimpaCpf));

        var r = await new ClassificarDocumentoTerceirizadoQueryHandler(new ExtratorFalso(new string('x', 60)),
                new ClassificadorFalso(Lido("aso", asoTipo: "periodico", asoResultado: "apto_restricao")), db)
            .Handle(new ClassificarDocumentoTerceirizadoQuery("aso.pdf", "application/pdf", new byte[] { 1 }), default);

        Assert.Equal(TipoExameAso.Periodico, r.AsoTipo);
        Assert.Equal(ResultadoAso.AptoComRestricao, r.AsoResultado);
        Assert.Equal("52998224725", r.PessoaCpf);
        Assert.Null(r.DocumentoExigidoId);
    }

    [Fact]
    public async Task Certificado_CursoInexistente_FicaSemIdMasComNomeLido()
    {
        var db = CriarDb(nameof(Certificado_CursoInexistente_FicaSemIdMasComNomeLido));
        db.CursosTreinamento.Add(new CursoTreinamento { Nome = "NR-35 Trabalho em Altura" });
        await db.SaveChangesAsync();

        var r = await new ClassificarDocumentoTerceirizadoQueryHandler(new ExtratorFalso(new string('x', 60)),
                new ClassificadorFalso(Lido("certificado", curso: "NR-33 Espaço Confinado")), db)
            .Handle(new ClassificarDocumentoTerceirizadoQuery("nr33.pdf", "application/pdf", new byte[] { 1 }), default);

        Assert.Null(r.CursoTreinamentoId);
        Assert.Equal("NR-33 Espaço Confinado", r.CursoNome);
    }

    [Fact]
    public async Task Imagem_VaiComoImagemSemExtrairTexto()
    {
        var db = CriarDb(nameof(Imagem_VaiComoImagemSemExtrairTexto));
        var classificador = new ClassificadorFalso(Lido("certificado"));

        await new ClassificarDocumentoTerceirizadoQueryHandler(new ExtratorFalso(), classificador, db)
            .Handle(new ClassificarDocumentoTerceirizadoQuery("foto.jpg", "image/jpeg", new byte[] { 9, 9 }), default);

        Assert.Equal(new byte[] { 9, 9 }, classificador.ImagemRecebida);
    }

    [Fact]
    public async Task FormatoNaoSuportado_NaoChamaIa()
    {
        var db = CriarDb(nameof(FormatoNaoSuportado_NaoChamaIa));
        var classificador = new ClassificadorFalso(Lido("aso"));

        var r = await new ClassificarDocumentoTerceirizadoQueryHandler(new ExtratorFalso(), classificador, db)
            .Handle(new ClassificarDocumentoTerceirizadoQuery("planilha.xlsx", "application/vnd.ms-excel", new byte[] { 1 }), default);

        Assert.Equal("outro", r.Tipo);
        Assert.Equal(0, classificador.Chamadas);
    }
}
