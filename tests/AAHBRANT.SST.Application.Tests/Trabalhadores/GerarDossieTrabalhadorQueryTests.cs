using System.Text;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.EntregasEpi.Queries;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Application.TermosCompromissoEpi.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

// Dossiê do Funcionário: relatório + índice + documentos, na ordem das seções, sem duplicar o que a
// Ficha de EPI já traz, e sem cair inteiro quando um documento não sai.
public class GerarDossieTrabalhadorQueryTests
{
    private static byte[] B(string s) => Encoding.ASCII.GetBytes(s);
    private static string S(byte[] b) => Encoding.ASCII.GetString(b);

    private sealed class MescladorFalso : IPdfMesclador
    {
        public List<List<string>> Chamadas { get; } = new();

        public byte[] Mesclar(IReadOnlyList<byte[]> pdfs)
        {
            var nomes = pdfs.Select(S).ToList();
            Chamadas.Add(nomes);
            if (nomes.Contains("DANIFICADO")) throw new InvalidOperationException("qpdf: arquivo inválido");
            return B(string.Join("+", nomes));
        }
    }

    private sealed class PdfFalso : IDossieTrabalhadorPdfService
    {
        public List<ItemIndiceDossie> Indice { get; private set; } = new();

        public byte[] GerarIndice(string trabalhadorNome, string obraNome, byte[]? obraLogo, IReadOnlyList<ItemIndiceDossie> itens)
        {
            Indice = itens.ToList();
            return B("INDICE");
        }

        public byte[]? ImagemComoPdf(string titulo, byte[] imagem, string obraNome, byte[]? obraLogo) => B("IMAGEM");
    }

    private static async Task<(Guid TrabalhadorId, AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db)> SemearAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var trabalhador = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        var curso = new CursoTreinamento { Nome = "NR-35" };
        db.Obras.Add(obra);
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);
        db.Treinamentos.Add(new Treinamento { TrabalhadorId = trabalhador.Id, CursoTreinamentoId = curso.Id, DataRealizacao = new DateTime(2026, 8, 1) });
        db.EntregasEpi.Add(new EntregaEpi { TrabalhadorId = trabalhador.Id, CatalogoEpiId = Guid.NewGuid(), DataEntrega = new DateTime(2026, 7, 2) });

        void Assinado(string tipo, DateTime quando)
        {
            var doc = new DocumentoAssinatura { EntidadeTipo = tipo, EntidadeId = Guid.NewGuid() };
            doc.Signatarios.Add(new DocumentoSignatario { DocumentoAssinaturaId = doc.Id, TrabalhadorId = trabalhador.Id, AssinadoEm = quando });
            db.DocumentosAssinatura.Add(doc);
        }
        Assinado("Dds", new DateTime(2026, 10, 28));
        Assinado("EntregaEpi", new DateTime(2026, 7, 2));
        Assinado("Inspecao", new DateTime(2026, 9, 2));
        await db.SaveChangesAsync();
        return (trabalhador.Id, db);
    }

    private static Func<object, object?> Respostas(string pdfDds = "DDS") => req => req switch
    {
        GerarRelatorioFiscalizacaoTrabalhadorQuery => B("RELATORIO"),
        ExportarCertificadoTreinamentoQuery => B("CERTIFICADO"),
        ExportarDdsPdfQuery => B(pdfDds),
        ExportarInspecaoPdfQuery => null, // fora do escopo de quem baixa
        ExportarFichaEpiTrabalhadorQuery => B("FICHA"),
        ObterArquivoTermoManualEpiQuery => null,
        _ => throw new InvalidOperationException($"Consulta inesperada: {req.GetType().Name}"),
    };

    [Fact]
    public async Task Gera_RelatorioIndiceEDocumentos_NaOrdemDasSecoes()
    {
        var (id, db) = await SemearAsync();
        var mediator = new MediatorSimulado(Respostas());
        var mesclador = new MescladorFalso();
        var pdf = new PdfFalso();

        var resultado = await new GerarDossieTrabalhadorQueryHandler(db, mediator, mesclador, pdf)
            .Handle(new GerarDossieTrabalhadorQuery(id), CancellationToken.None);

        Assert.Equal("RELATORIO+INDICE+CERTIFICADO+DDS+FICHA", S(resultado!));
        // A entrega de EPI assinada já está dentro da Ficha: não é exportada de novo.
        Assert.DoesNotContain(mediator.Requisicoes, r => r.GetType().Name.Contains("Entrega", StringComparison.Ordinal) && r is not ExportarFichaEpiTrabalhadorQuery);
        var inspecao = Assert.Single(pdf.Indice, i => i.Titulo == "Inspeção de Segurança");
        Assert.False(inspecao.Incluido);
        Assert.NotNull(inspecao.Observacao);
    }

    [Fact]
    public async Task DocumentoDanificado_FicaForaEMarcadoNoIndice_SemDerrubarODossie()
    {
        var (id, db) = await SemearAsync();
        var mesclador = new MescladorFalso();
        var pdf = new PdfFalso();

        var resultado = await new GerarDossieTrabalhadorQueryHandler(db, new MediatorSimulado(Respostas(pdfDds: "DANIFICADO")), mesclador, pdf)
            .Handle(new GerarDossieTrabalhadorQuery(id), CancellationToken.None);

        Assert.Equal("RELATORIO+INDICE+CERTIFICADO+FICHA", S(resultado!));
        var dds = Assert.Single(pdf.Indice, i => i.Secao.StartsWith("DDS", StringComparison.Ordinal));
        Assert.False(dds.Incluido);
    }

    [Fact]
    public async Task ForaDoEscopo_DevolveNulo()
    {
        var (id, db) = await SemearAsync();
        var mediator = new MediatorSimulado(req => req is GerarRelatorioFiscalizacaoTrabalhadorQuery ? null : throw new InvalidOperationException());

        var resultado = await new GerarDossieTrabalhadorQueryHandler(db, mediator, new MescladorFalso(), new PdfFalso())
            .Handle(new GerarDossieTrabalhadorQuery(id), CancellationToken.None);

        Assert.Null(resultado);
    }
}
