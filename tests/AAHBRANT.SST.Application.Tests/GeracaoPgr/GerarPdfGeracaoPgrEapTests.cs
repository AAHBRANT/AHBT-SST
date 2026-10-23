using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// PDF do PGR gerado pela EAP (23/10/2026): só depois da aprovação do Engenheiro de Segurança.
public class GerarPdfGeracaoPgrEapTests
{
    private sealed class PdfFalso : IPgrPdfService
    {
        public DadosPgrPdf? Dados { get; private set; }

        public byte[] Gerar(DadosPgrPdf dados, byte[]? logoObra)
        {
            Dados = dados;
            return new byte[] { 1, 2, 3 };
        }
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, GeracaoPgrEap Geracao)> CriarAsync(StatusGeracaoPgrEap status, string estruturaJson)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste", Cnpj = "00.000.000/0001-00" };
        var geracao = new GeracaoPgrEap
        {
            Obra = obra,
            Nome = "PGR Obra Teste",
            DataElaboracao = new DateTime(2026, 10, 21),
            NomeArquivoEap = "eap.xlsx",
            Status = status,
            EstruturaJson = estruturaJson,
            AprovadoPorNome = "Eng. Carlos",
            AprovadoEmUtc = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
        };
        db.AddRange(obra, geracao);
        await db.SaveChangesAsync();
        return (db, geracao);
    }

    [Fact]
    public async Task NaoGera_PdfEnquantoNaoAprovado()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.AguardandoAprovacao, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GerarPdfGeracaoPgrEapQueryHandler(db, new PdfFalso()).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default));
    }

    [Fact]
    public async Task GeraPdf_ComDadosDaEstruturaAprovada_EMesmoNomeDoArquivo()
    {
        const string estrutura = """
            {"Ghes":[{"Numero":1,"Nome":"Estrutura","Funcoes":[{"Nome":"ARMADOR","Descricao":"Monta armaduras"}],
            "Atividades":[{"CodigoEap":"1.1","Nome":"Concretagem de laje","RiscosGerados":true,
            "Riscos":[{"Tipo":"Acidente","Perigo":"Queda de altura","Probabilidade":4,"Severidade":5,"Revisado":true,
            "Medidas":[{"Fase":1,"Texto":"Instale guarda-corpo","Decisao":"Aceita"}]}]}]}],"PlanoAcao":["Instalar linha de vida"]}
            """;
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, estrutura);
        var pdf = new PdfFalso();

        var arquivo = await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        Assert.Equal(new byte[] { 1, 2, 3 }, arquivo.Conteudo);
        Assert.Equal("PGR - Obra Teste - 20261021.pdf", arquivo.NomeArquivo);
        Assert.Equal("Obra Teste", pdf.Dados!.ObraNome);
        Assert.Equal("00.000.000/0001-00", pdf.Dados.ObraCnpj);
        Assert.Equal("Eng. Carlos", pdf.Dados.AprovadoPorNome);
        Assert.Equal("Concretagem de laje", pdf.Dados.Estrutura.Ghes.Single().Atividades.Single().Nome);
        Assert.Equal("Instalar linha de vida", pdf.Dados.Estrutura.PlanoAcao.Single());
    }
}
