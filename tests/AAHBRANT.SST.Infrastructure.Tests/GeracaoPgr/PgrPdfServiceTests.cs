using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.GeracaoPgr;

// Layout do PDF do PGR gerado pela EAP (23/10/2026): deve sair um PDF válido com e sem logo da obra.
public class PgrPdfServiceTests
{
    static PgrPdfServiceTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static DadosPgrPdf DadosCompletos() => new(
        ObraNome: "Obra Teste",
        ObraCnpj: "00.000.000/0001-00",
        Nome: "PGR Obra Teste",
        DataElaboracao: new DateTime(2026, 10, 21),
        DataProximaRevisao: new DateTime(2027, 10, 21),
        ElaboradoPorNome: "Téc. Ana",
        NomeArquivoEap: "eap.xlsx",
        AprovadoPorNome: "Eng. Carlos",
        AprovadoEmUtc: new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
        Estrutura: new EstruturaGeracaoPgr
        {
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1, Nome = "Estrutura", Jornada = "8h/dia",
                    Funcoes = new() { new FuncaoGerada { Nome = "ARMADOR", Descricao = "Monta armaduras" } },
                    Atividades = new()
                    {
                        new AtividadeGerada
                        {
                            CodigoEap = "1.1", Nome = "Concretagem de laje", RiscosGerados = true,
                            Riscos = new()
                            {
                                new RiscoGerado
                                {
                                    Tipo = "Acidente", Perigo = "Queda de altura", Danos = "Fraturas", Probabilidade = 4, Severidade = 5, Revisado = true,
                                    Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Decisao = "Aceita" } },
                                },
                                new RiscoGerado { Perigo = "Excluído", Probabilidade = 1, Severidade = 1, Excluido = true },
                            },
                        },
                    },
                },
            },
            PlanoAcao = new() { "Instalar linha de vida" },
        },
        GeradoEmUtc: new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Gerar_SemLogo_DevolvePdfValido()
    {
        var pdf = new PgrPdfService().Gerar(DadosCompletos(), null);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Gerar_SemRiscosNemAprovacao_AindaDevolvePdfValido()
    {
        var dados = DadosCompletos() with { AprovadoPorNome = null, AprovadoEmUtc = null, Estrutura = new EstruturaGeracaoPgr() };

        var pdf = new PgrPdfService().Gerar(dados, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
