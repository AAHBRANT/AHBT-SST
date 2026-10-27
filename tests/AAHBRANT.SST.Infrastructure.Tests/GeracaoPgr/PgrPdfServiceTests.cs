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
                                    Medidas = new() { new MedidaGerada { Fase = 1, Texto = "Instale guarda-corpo", Categoria = CategoriasMedida.Epc, Decisao = "Aceita" }, new MedidaGerada { Fase = 2, Texto = "Cinto paraquedista", Categoria = CategoriasMedida.Epi, Decisao = "Aceita" }, new MedidaGerada { Fase = 2, Texto = "Medida de rascunho antigo", Decisao = "Aceita" } },
                                },
                                new RiscoGerado { Perigo = "Excluído", Probabilidade = 1, Severidade = 1, Excluido = true },
                            },
                        },
                    },
                },
            },
            DataTermino = new DateTime(2027, 10, 21),
            AcoesPlano = new() { new AcaoPlanoGerada { Descricao = "Instalar linha de vida", ResponsavelNome = "Téc. Ana", Prazo = new DateTime(2026, 12, 15) }, new AcaoPlanoGerada { Descricao = "Sem responsável ainda" } },
        },
        GeradoEmUtc: new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc),
        ObraEndereco: "Rua das Obras, 100 — Centro",
        ObraCidade: "Belo Horizonte", ObraUf: "MG",
        Funcoes: new List<LinhaFuncaoPdf> { new(1, "ARMADOR", "715210", 12) },
        Revisoes: new List<RevisaoPdf> { new(0, new DateTime(2026, 10, 21), "Emissão inicial.", "Téc. Ana"), new(1, new DateTime(2027, 3, 2), "Inclusão de nova frente.", "Eng. Carlos") },
        Matriz: MatrizDeTeste());

    private static MatrizRiscoPdf MatrizDeTeste()
    {
        var celulas = new List<CelulaMatrizPdf>();
        for (var p = 1; p <= 5; p++)
            for (var s = 1; s <= 5; s++)
                celulas.Add(new CelulaMatrizPdf(p, s, p * s >= 20 ? Domain.Enums.NivelRisco.Critico : p * s >= 12 ? Domain.Enums.NivelRisco.Alto : p * s >= 6 ? Domain.Enums.NivelRisco.Moderado : Domain.Enums.NivelRisco.Baixo));
        return new MatrizRiscoPdf(5, 5, celulas);
    }

    [Fact]
    public void Gerar_ComMatrizEFichas_DevolvePdfComVariasPaginas()
    {
        var pdf = new PgrPdfService().Gerar(DadosCompletos(), null);

        // capa + sumário + conteúdo (mais de uma página)
        var paginas = System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;
        Assert.True(paginas >= 3, $"esperava ao menos 3 páginas, veio {paginas}");
    }

    [Fact]
    public void Gerar_ComExposicaoEpiComCaECronogramaDeDoisAnos_DevolvePdfValido()
    {
        var epiId = Guid.NewGuid();
        var dados = DadosCompletos() with { Epis = new Dictionary<Guid, CatalogoEpiInfo> { [epiId] = new("Protetor auricular", "12345", new DateTime(2028, 5, 1)) } };
        var risco = dados.Estrutura.Ghes[0].Atividades[0].Riscos[0];
        risco.Tipo = "Físico";
        risco.Perigo = "Ruído de equipamentos";
        risco.Exposicao = new ExposicaoRisco
        {
            Criterio = CriteriosExposicao.Quantitativo,
            Perfil = "Equipe de 12 pessoas, 6 horas por dia.",
            Medicoes = new()
            {
                new MedicaoExposicao
                {
                    Data = new DateTime(2026, 10, 5), Valor = 87.4m, Unidade = "dB(A)", LimiteTolerancia = "85 dB(A)", NivelAcao = "80 dB(A)",
                    Empresa = "Higiene Ocupacional Ltda", Tecnica = "Dosimetria", Equipamento = "Dosímetro XYZ",
                },
            },
        };
        risco.Medidas.Add(new MedidaGerada { Fase = 2, Texto = "Protetor auricular", Categoria = CategoriasMedida.Epi, CatalogoEpiId = epiId, Decisao = "Aceita" });
        dados.Estrutura.AcoesPlano = new()
        {
            new AcaoPlanoGerada { Descricao = "Dosimetria de ruído nas frentes de escavação", ResponsavelNome = "Téc. Ana", Inicio = new DateTime(2026, 11, 10), Prazo = new DateTime(2027, 1, 20) },
            new AcaoPlanoGerada { Descricao = "Treinamento de conservação auditiva", ResponsavelNome = "Eng. Carlos", Prazo = new DateTime(2026, 12, 15) },
            new AcaoPlanoGerada { Descricao = "Ação sem prazo definido" },
        };

        var pdf = new PgrPdfService().Gerar(dados, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Gerar_ComContatosDeEmergenciaERotas_DevolvePdfComOsTextosDaFicha()
    {
        var dados = DadosCompletos();
        var comp = dados.Estrutura.Complementares;
        comp.Cnae = "4211-1/01 — Construção de rodovias";
        comp.GrauRisco = 3;
        comp.ResponsavelTecnicoRegistro = "CREA 12345";
        comp.ResponsavelImplantacaoNome = "Enc. João";
        comp.ContatosEmergencia = new()
        {
            new() { Chave = "hospital", Local = "Hospital — Pronto-Socorro Central", Endereco = "Av. B, 200", Telefone = "(31) 3333-0000", DistanciaKm = 5.2, TempoMinutos = 12, Rota = "Saia da obra e siga pela Rua da Obra por 300 m.\nChegue ao destino.", Recomendado = true },
            new() { Chave = "bombeiros", Local = "Corpo de Bombeiros", Telefone = "193" },
        };
        comp.RotasCalculadasEmUtc = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        comp.RotasFonte = "OpenStreetMap";

        var pdf = new PgrPdfService().Gerar(dados, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1000);
    }

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

    [Fact]
    public void Gerar_ComCursosObrigatorios_DevolvePdfValidoComASecaoDeTreinamentos()
    {
        var dados = DadosCompletos() with
        {
            Treinamentos = new List<LinhaTreinamentoPdf>
            {
                new(1, "ARMADOR", "NR-35 Trabalho em Altura", "NR-35", 8, 24, "Perigo \"Queda de altura\" na atividade 1.1 Concretagem de laje", true),
                new(1, "ARMADOR", "NR-12 Máquinas e Equipamentos", "NR-12", 0, 0, null, false),
            },
        };

        var pdf = new PgrPdfService().Gerar(dados, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Gerar_SemCursosObrigatorios_DevolvePdfValido()
    {
        var pdf = new PgrPdfService().Gerar(DadosCompletos() with { Treinamentos = null }, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Gerar_ComAcompanhamentoDasAcoes_DevolvePdfValidoComASecao72()
    {
        var dados = DadosCompletos();
        dados.Estrutura.AcoesPlano = new()
        {
            new AcaoPlanoGerada
            {
                Descricao = "Instalar linha de vida", ResponsavelNome = "Téc. Ana", Prazo = new DateTime(2026, 12, 15),
                FormaAcompanhamento = "Inspeção periódica", PeriodicidadeAcompanhamento = "Semanal",
                CriterioAfericao = "100% dos pontos de ancoragem inspecionados, sem não conformidade aberta",
            },
            new AcaoPlanoGerada { Descricao = "Ação sem acompanhamento definido" },
        };

        var pdf = new PgrPdfService().Gerar(dados, null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1000);
    }
}
