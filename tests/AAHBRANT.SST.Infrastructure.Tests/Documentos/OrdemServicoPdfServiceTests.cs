using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

public class OrdemServicoPdfServiceTests
{
    static OrdemServicoPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void Gerar_OrdemServicoCompleta_ComLogoDaObraEAtividades()
    {
        using var qrGen = new QRCoder.QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode("https://sst.aahbrant.com.br/#/validar/os-2026-0042", QRCoder.QRCodeGenerator.ECCLevel.M);
        var qrPng = new QRCoder.PngByteQRCode(qrData).GetGraphic(10);

        byte[]? logoObra = null;
        var caminhoLogo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AAHBRANT.SST.Infrastructure", "Documentos", "Assets", "logo-aahbrant.png");
        if (File.Exists(caminhoLogo))
            logoObra = File.ReadAllBytes(caminhoLogo);

        var minuta = new MinutaOrdemServicoDto
        {
            TrabalhadorId = Guid.NewGuid(),
            Obra = new ObraOsDto
            {
                Id = Guid.NewGuid(),
                Nome = "Obra Beira Rio (CBR)",
                Cliente = "Consórcio Beira Rio",
                Cnpj = "12.345.678/0001-90",
                Endereco = "Av. Beira Rio, s/n — Canteiro Central — Cuiabá/MT",
            },
            Trabalhador = new TrabalhadorOsDto
            {
                Nome = "Antony Adriel Francisco da Silva",
                Matricula = "00418",
                Cpf = "123.456.789-40",
                CpfMascarado = "***.***.***-40",
                Funcao = "Ajudante Geral",
                Cbo = "7170-20",
                DataAdmissao = new DateTime(2026, 11, 10),
            },
            Pgr = new PgrOsDto(Guid.NewGuid(), "PGR Obra Beira Rio (CBR)", new DateTime(2026, 11, 1), 1),
            Ghes = new List<GheOsDto>
            {
                new(1, "Auxilia nas frentes de serviços preliminares, limpeza, organização de materiais, sinalização de áreas e apoio às equipes de campo sob supervisão."),
                new(2, "Auxilia no transporte manual de ferramentas, isolamento de frentes de terraplenagem e suporte operacional em solo."),
            },
            Atividades = new List<AtividadeOsDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Nome = "01.01.2 Serviços Preliminares e Mobilização de Canteiro",
                    Condicoes = "Ambiente: Canteiro de obras a céu aberto\nJornada: 44 horas semanais",
                    Ferramentas = new List<string> { "Pá", "Enxada", "Carrinho de mão", "Vassoura industrial" },
                    Epcs = new List<string> { "Cones e fita zebrada de isolamento", "Telas tapume de sinalização" },
                    Procedimentos = new List<string> { "IT-SSO-01.01.2 — IT — 01.01.2 Serviços Preliminares (rev. 01)" },
                    Riscos = new List<RiscoOsDto>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Perigo = "Projeção de partículas e poeira mineral",
                            Nivel = NivelRisco.Moderado,
                            Consequencia = "Irritação ocular e respiratória",
                            Antes = new List<string> { "Inspecionar óculos de segurança e respirador PFF2 antes de iniciar a atividade." },
                            Durante = new List<string> { "Utilizar óculos de proteção e máscara PFF2 durante toda a limpeza e movimentação de agregados.", "Umedecer o local quando houver geração excessiva de poeira." },
                            Apos = new List<string> { "Higienizar os óculos de proteção e descartar o respirador saturado em local adequado." },
                        },
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Perigo = "Circulação de máquinas e veículos pesados no canteiro",
                            Nivel = NivelRisco.Alto,
                            Consequencia = "Atropelamento e prensamento",
                            Antes = new List<string> { "Verificar isolamento da área e rotas de pedestres sinalizadas." },
                            Durante = new List<string> { "Manter distância segura do raio de giro dos equipamentos e usar colete refletivo.", "Nunca permanecer no ponto cego de caminhões ou retroescavadeiras." },
                            Apos = new List<string> { "Manter ferramentas recolhidas fora das vias de circulação." },
                        },
                    },
                    InterromperQuando = new List<string> { "Houver falha no isolamento da área ou aproximação não sinalizada de máquinas pesadas." },
                    Restricoes = new List<RestricaoOsDto> { new("Operar equipamentos motorizados ou intervir em instalações elétricas", "Exige qualificação específica e autorização formal") },
                },
            },
            Epis = new List<EpiOsDto>
            {
                new("Capacete de segurança com jugular", "31469"),
                new("Botina de segurança com biqueira de composite", "42115"),
                new("Óculos de segurança incolor/cinza", "11268"),
                new("Luva de segurança pigmentada / vaqueta", "38512"),
                new("Protetor auricular tipo plug / concha", "14235"),
                new("Respirador semifacial PFF2", "39014"),
            },
            Treinamentos = new List<TreinamentoOsDto>
            {
                new("NR-06 — Uso e Conservação de EPI", "NR-06", new DateTime(2027, 11, 10), "Valido"),
                new("NR-18 — Condições e Meio Ambiente de Trabalho na Indústria da Construção", "NR-18", new DateTime(2027, 11, 10), "Valido"),
            },
            Aso = new AsoOsDto(ResultadoAso.Apto, new DateTime(2027, 10, 30), true),
            Politicas = new List<PoliticaOsDto>
            {
                new("Política de Saúde e Segurança do Trabalho", "POL-SSO-001", "02", "Mural do canteiro e G-SST"),
            },
        };

        var emissao = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var modelo = new OrdemServicoPdfModelo(
            minuta,
            "OS-2026-0042",
            1,
            emissao,
            logoObra,
            SituacaoOrdemServico.Valida,
            new AssinaturaOsDto("Antony Adriel Francisco da Silva", "Ajudante Geral", emissao.AddMinutes(5), MetodoAutenticacaoAssinatura.Biometria),
            new AssinaturaOsDto("Carlos Eduardo Mendes", "Técnico de Segurança do Trabalho", emissao.AddMinutes(8), MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            "8F4B2C9A1D7E6F0345A1B2C3D4E5F60718293A4B5C6D7E8F90123456789ABCDE",
            "https://sst.aahbrant.com.br/#/validar/os-2026-0042",
            qrPng);

        var pdf = new OrdemServicoPdfService().Gerar(modelo);
        Assert.True(pdf.Length > 1000);

        var pasta = Environment.GetEnvironmentVariable("FICHA_PDF_SAIDA");
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, "ordem-servico-exemplo.pdf"), pdf);
        }
    }
}
