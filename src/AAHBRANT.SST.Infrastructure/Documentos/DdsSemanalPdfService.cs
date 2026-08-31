using AAHBRANT.SST.Application.Dds;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Replica o "Registro Semanal de Diálogo Diário de Segurança - DDS" (documento em papel enviado
// pelo usuário em 31/08): cabeçalho com dados da semana, grade Seg-Sex com tema+data de cada dia,
// tabela única de presença (um trabalhador por linha, uma coluna de rubrica por dia) e assinaturas
// de encerramento no rodapé — 2 blocos para Empregados Próprios, 3 para Terceirizados.
public class DdsSemanalPdfService : IDdsSemanalPdfService
{
    private const string CorMarca = "#670000";
    private static readonly string[] NomesDias = { "SEGUNDA-FEIRA", "TERÇA-FEIRA", "QUARTA-FEIRA", "QUINTA-FEIRA", "SEXTA-FEIRA" };
    private static readonly string[] SiglasDias = { "SEG", "TER", "QUA", "QUI", "SEX" };

    public byte[] Gerar(DdsSemanalPdfModelo modelo)
    {
        var terceirizados = !string.IsNullOrWhiteSpace(modelo.EmpresaTerceirizada);

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(
                        coluna,
                        $"Registro Semanal de DDS — {modelo.TipoLabel}",
                        modelo.ObraNome,
                        modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(10);
                    coluna.Item().Element(c => DesenharCabecalhoDados(c, modelo, terceirizados));
                    coluna.Item().Element(c => DesenharGradeDias(c, modelo));
                    coluna.Item().Element(c => DesenharTabelaPresenca(c, modelo));
                    coluna.Item().PaddingTop(4).Text(
                        "Registro de participação: a rubrica/assinatura nas colunas SEG a SEX comprova a presença do " +
                        "trabalhador no DDS do respectivo dia. O responsável deve registrar o tema efetivamente abordado " +
                        "e manter este documento arquivado como evidência de orientação de segurança.")
                        .FontSize(7.5f).Italic();
                    coluna.Item().PaddingTop(10).Element(c => DesenharAssinaturas(c, modelo, terceirizados));
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(8);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static void DesenharCabecalhoDados(IContainer container, DdsSemanalPdfModelo modelo, bool terceirizados)
    {
        var semana = $"{modelo.DataInicioSemana:dd/MM/yyyy} a {modelo.DataFimSemana:dd/MM/yyyy}";

        container.Border(1).BorderColor(CorMarca).Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn();
                colunas.RelativeColumn();
                colunas.RelativeColumn();
            });

            if (terceirizados)
            {
                CelulaRotulo(tabela, "Empresa contratante:", modelo.ObraNome ?? "AAHBRANT");
                CelulaRotulo(tabela, "Empresa terceirizada:", modelo.EmpresaTerceirizada ?? "-");
                CelulaRotulo(tabela, "Nº do documento:", modelo.NumeroDocumento ?? "-");

                CelulaRotulo(tabela, "Obra / Contrato:", modelo.ObraNome ?? "-");
                CelulaRotulo(tabela, "Local / Frente de serviço:", modelo.LocalFrenteServico ?? "-");
                CelulaRotulo(tabela, "Semana:", semana);
            }
            else
            {
                CelulaRotulo(tabela, "Empresa:", modelo.ObraNome ?? "AAHBRANT");
                CelulaRotulo(tabela, "Obra / Contrato:", modelo.ObraNome ?? "-");
                CelulaRotulo(tabela, "Nº do documento:", modelo.NumeroDocumento ?? "-");

                CelulaRotulo(tabela, "Local / Frente de serviço:", modelo.LocalFrenteServico ?? "-");
                CelulaRotulo(tabela, "Responsável pelo DDS:", modelo.ResponsavelNome);
                CelulaRotulo(tabela, "Semana:", semana);
            }
        });
    }

    private static void CelulaRotulo(TableDescriptor tabela, string rotulo, string valor)
    {
        tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(c =>
        {
            c.Item().Text(rotulo).FontSize(7.5f).SemiBold().FontColor(CorMarca);
            c.Item().Text(valor).FontSize(9);
        });
    }

    private static void DesenharGradeDias(IContainer container, DdsSemanalPdfModelo modelo)
    {
        container.Row(linha =>
        {
            foreach (var nomeDia in NomesDias)
            {
                var indice = Array.IndexOf(NomesDias, nomeDia);
                var dia = modelo.Dias.Count > indice ? modelo.Dias[indice] : null;

                linha.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(c =>
                {
                    c.Item().AlignCenter().Text(nomeDia).FontSize(7.5f).Bold().FontColor(CorMarca);
                    c.Item().AlignCenter().Text(dia is not null ? dia.Data.ToString("dd/MM") : "-").FontSize(8);
                    c.Item().PaddingTop(4).Text("Tema do DDS:").FontSize(7).SemiBold();
                    c.Item().PaddingTop(2).MinHeight(24).Text(dia?.Tema ?? "—").FontSize(8);
                });
            }
        });
    }

    private static void DesenharTabelaPresenca(IContainer container, DdsSemanalPdfModelo modelo)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.ConstantColumn(22);
                colunas.RelativeColumn(3);
                colunas.RelativeColumn(2);
                colunas.RelativeColumn(2);
                for (var i = 0; i < 5; i++)
                    colunas.ConstantColumn(32);
            });

            tabela.Header(cabecalho =>
            {
                void Cabecalho(string texto) => cabecalho.Cell().Background(CorMarca).Padding(3).AlignCenter()
                    .Text(texto).FontSize(7).Bold().FontColor(Colors.White);

                Cabecalho("Nº");
                Cabecalho("NOME COMPLETO DO TRABALHADOR");
                Cabecalho("FUNÇÃO");
                Cabecalho("MATRÍCULA");
                foreach (var sigla in SiglasDias)
                    Cabecalho(sigla);
            });

            var numero = 1;
            foreach (var presenca in modelo.Presencas)
            {
                CelulaCorpo(tabela, numero.ToString("00"));
                CelulaCorpo(tabela, presenca.Nome);
                CelulaCorpo(tabela, presenca.Funcao);
                CelulaCorpo(tabela, presenca.Matricula);
                for (var i = 0; i < 5; i++)
                {
                    var presente = i < presenca.PresencaPorDia.Count && presenca.PresencaPorDia[i];
                    tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).AlignCenter().AlignMiddle()
                        .Text(presente ? "X" : "-").FontSize(8).FontColor(presente ? CorMarca : Colors.Grey.Lighten1);
                }
                numero++;
            }

            var linhasRestantes = Math.Max(0, 15 - modelo.Presencas.Count);
            for (var i = 0; i < linhasRestantes; i++)
            {
                CelulaCorpo(tabela, numero.ToString("00"));
                CelulaCorpo(tabela, string.Empty);
                CelulaCorpo(tabela, string.Empty);
                CelulaCorpo(tabela, string.Empty);
                for (var d = 0; d < 5; d++)
                    tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).MinHeight(16).Text(string.Empty);
                numero++;
            }
        });
    }

    private static void CelulaCorpo(TableDescriptor tabela, string texto)
    {
        tabela.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).MinHeight(16)
            .Text(texto).FontSize(8);
    }

    private static void DesenharAssinaturas(IContainer container, DdsSemanalPdfModelo modelo, bool terceirizados)
    {
        container.Row(linha =>
        {
            linha.RelativeItem().Element(c => BlocoAssinatura(c, "RESPONSÁVEL / TREINADOR PELO DDS", modelo.ResponsavelNome, null));

            if (terceirizados)
                linha.RelativeItem().Element(c => BlocoAssinatura(
                    c, "RESPONSÁVEL DA EMPRESA TERCEIRIZADA",
                    modelo.ResponsavelEmpresaTerceirizadaNome, modelo.ResponsavelEmpresaTerceirizadaFuncao));

            linha.RelativeItem().Element(c => BlocoAssinatura(c, "RESPONSÁVEL DA OBRA / SST", modelo.ResponsavelObraSstNome, null));
        });
    }

    private static void BlocoAssinatura(IContainer container, string titulo, string? nome, string? funcao)
    {
        container.PaddingHorizontal(6).Column(c =>
        {
            c.Item().Text(titulo).FontSize(7.5f).Bold().FontColor(CorMarca);
            c.Item().PaddingTop(4).Text(t =>
            {
                t.Span("Nome: ").SemiBold().FontSize(8);
                t.Span(nome ?? "—").FontSize(8);
            });
            c.Item().PaddingTop(2).Text(t =>
            {
                t.Span("Função: ").SemiBold().FontSize(8);
                t.Span(funcao ?? "—").FontSize(8);
            });
            c.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            c.Item().AlignCenter().Text("Assinatura").FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }
}
