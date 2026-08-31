using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Ficha consolidada por trabalhador, alinhada ao modelo oficial AHBT-FIC-SSO-XXX-00 (docs/superpowers/
// specs/2026-08-27-ficha-epi-reformulada-design.md) — identificação, termo de compromisso, tabela de
// entregas e tabela de devoluções, em vez do PDF por entrega individual gerado anteriormente.
public class EntregaEpiPdfService : IFichaEpiPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(FichaEpiPdfModelo modelo)
    {
        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Ficha de Controle e Entrega de EPI", modelo.ObraNome, modelo.ObraLogoConteudo));

                pagina.Content().PaddingVertical(10).Column(coluna =>
                {
                    coluna.Spacing(10);

                    coluna.Item().Element(c => SecaoIdentificacao(c, modelo));
                    coluna.Item().Element(SecaoTermoCompromisso(modelo));
                    coluna.Item().Element(c => SecaoControleEntrega(c, modelo));
                    coluna.Item().Element(c => SecaoControleDevolucao(c, modelo));
                    coluna.Item().Element(SecaoObservacao);
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

    private static void SecaoIdentificacao(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("1. Identificação do Trabalhador").FontSize(11).Bold().FontColor(CorMarca);

            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("Nome completo: ").SemiBold(); t.Span(modelo.TrabalhadorNome); });
                linha.RelativeItem().Text(t => { t.Span("CPF: ").SemiBold(); t.Span(modelo.TrabalhadorCpfMascarado); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Matrícula: ").SemiBold(); t.Span(modelo.TrabalhadorMatricula); });
                linha.RelativeItem().Text(t => { t.Span("Função: ").SemiBold(); t.Span(modelo.TrabalhadorFuncaoNome); });
                linha.RelativeItem().Text(t => { t.Span("Turno: ").SemiBold(); t.Span(modelo.TrabalhadorTurno ?? "não informado"); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem().Text(t => { t.Span("Data de admissão: ").SemiBold(); t.Span(modelo.TrabalhadorDataAdmissao.ToString("dd/MM/yyyy")); });
                linha.RelativeItem(2).Text(t => { t.Span("Obra / Frente de trabalho: ").SemiBold(); t.Span(modelo.ObraNome); });
            });
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem(2).Text(t => { t.Span("Empresa contratante: ").SemiBold(); t.Span(modelo.ObraCliente ?? "não informado"); });
                linha.RelativeItem().Text(t => { t.Span("CNPJ da contratada: ").SemiBold(); t.Span(modelo.ObraCnpj ?? "não informado"); });
            });
        });
    }

    private static Action<IContainer> SecaoTermoCompromisso(FichaEpiPdfModelo modelo)
    {
        var contratante = modelo.ObraCliente ?? "empregador";
        return container => container.Column(coluna =>
        {
            coluna.Spacing(3);
            coluna.Item().Text("2. Termo de Recebimento e Compromisso de Uso").FontSize(11).Bold().FontColor(CorMarca);

            coluna.Item().Text($"1 — Declaro ter recebido do {contratante} os Equipamentos de Proteção Individual (EPIs) relacionados nesta ficha, nas datas e quantidades ali indicadas, todos em perfeitas condições de uso e com Certificado de Aprovação (CA) válido.");
            coluna.Item().Text("2 — Declaro ter recebido orientação e treinamento sobre o uso correto, a guarda, a conservação, a higienização e os critérios de substituição de cada EPI relacionado, conforme registrado na Lista de Presença de Treinamento (NR-6) nº __________, realizada em ___/___/______.");
            coluna.Item().Text("3 — Comprometo-me a utilizar os EPIs exclusivamente para a finalidade a que se destinam, durante toda a execução das minhas atividades laborais, zelando por sua guarda, conservação e higienização adequadas, e a comunicar imediatamente ao Setor de Segurança do Trabalho qualquer dano, extravio ou alteração que os torne impróprios para uso.");
            coluna.Item().Text("4 — Comprometo-me a devolver os EPIs sempre que solicitado, inclusive nos casos de substituição, troca de função, mudança de atividade ou rescisão do meu contrato de trabalho.");
            coluna.Item().Text("5 — Estou ciente de que o descumprimento das obrigações aqui assumidas constitui falta funcional, passível de sanções disciplinares que poderão variar, a critério do empregador, de advertência por escrito até a rescisão contratual por justa causa, sem prejuízo de demais medidas legais cabíveis, conforme disposto no Art. 158 da CLT e na Norma Regulamentadora nº 6 (NR-6).");

            coluna.Item().PaddingTop(4).Text("Local: ______________________________     Data: ___/___/_______     Assinatura do Empregado — Termo de Compromisso: (ver assinaturas registradas por entrega, seção 3)").FontSize(8).Italic();
        });
    }

    private static void SecaoControleEntrega(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("3. Controle de Entrega de EPI").FontSize(11).Bold().FontColor(CorMarca);

            if (modelo.Entregas.Count == 0)
            {
                coluna.Item().Text("Nenhuma entrega registrada.").Italic();
                return;
            }

            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("Nº");
                    header.Cell().Element(CabecalhoCelula).Text("EPI");
                    header.Cell().Element(CabecalhoCelula).Text("CA");
                    header.Cell().Element(CabecalhoCelula).Text("Motivo");
                    header.Cell().Element(CabecalhoCelula).Text("Qtd.");
                    header.Cell().Element(CabecalhoCelula).Text("Data");
                    header.Cell().Element(CabecalhoCelula).Text("Assin. empregado");
                    header.Cell().Element(CabecalhoCelula).Text("Assin. responsável");
                });

                foreach (var linha in modelo.Entregas)
                {
                    table.Cell().Element(Celula).Text(linha.Numero.ToString());
                    table.Cell().Element(Celula).Text(linha.EpiNome);
                    table.Cell().Element(Celula).Text(linha.CertificadoAprovacaoNumero ?? "-");
                    table.Cell().Element(Celula).Text(MotivoLabel(linha.MotivoTipo, linha.MotivoObservacao));
                    table.Cell().Element(Celula).Text(linha.Quantidade.ToString());
                    table.Cell().Element(Celula).Text(linha.DataEntrega.ToString("dd/MM/yyyy"));
                    table.Cell().Element(Celula).Text(linha.AssinadoPeloEmpregado ? "Assinado" : "Pendente");
                    table.Cell().Element(Celula).Text(linha.AssinadoPeloResponsavel ? "Assinado" : "Pendente");
                }
            });
        });
    }

    private static void SecaoControleDevolucao(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("4. Controle de Devolução de EPI").FontSize(11).Bold().FontColor(CorMarca);

            if (modelo.Devolucoes.Count == 0)
            {
                coluna.Item().Text("Nenhuma devolução registrada.").Italic();
                return;
            }

            coluna.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("Nº (ref. entrega)");
                    header.Cell().Element(CabecalhoCelula).Text("EPI");
                    header.Cell().Element(CabecalhoCelula).Text("Qtd. devolvida");
                    header.Cell().Element(CabecalhoCelula).Text("Data");
                    header.Cell().Element(CabecalhoCelula).Text("Assin. empregado (devolução)");
                    header.Cell().Element(CabecalhoCelula).Text("Visto do responsável");
                });

                foreach (var linha in modelo.Devolucoes)
                {
                    table.Cell().Element(Celula).Text(linha.NumeroReferenciaEntrega.ToString());
                    table.Cell().Element(Celula).Text(linha.EpiNome);
                    table.Cell().Element(Celula).Text(linha.QuantidadeDevolvida.ToString());
                    table.Cell().Element(Celula).Text(linha.DataDevolucao.ToString("dd/MM/yyyy"));
                    table.Cell().Element(Celula).Text(linha.AssinadoPeloEmpregado ? "Assinado" : "Pendente");
                    table.Cell().Element(Celula).Text(linha.VistoResponsavel ?? "-");
                }
            });
        });
    }

    private static void SecaoObservacao(IContainer container)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("Observação").FontSize(11).Bold().FontColor(CorMarca);
            coluna.Item().Text("Antes de cada entrega, confirmar a validade do CA na Matriz de EPI por Função. Manter esta ficha arquivada por, no mínimo, 20 anos após o desligamento do trabalhador, para fins de rastreabilidade em fiscalizações e processos trabalhistas.").FontSize(8);
        });
    }

    private static IContainer CabecalhoCelula(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(3).BorderBottom(1).BorderColor(CorMarca).DefaultTextStyle(t => t.FontSize(7).Bold());

    private static IContainer Celula(IContainer container) =>
        container.Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).DefaultTextStyle(t => t.FontSize(7.5f));

    private static string MotivoLabel(MotivoEntregaEpi? motivo, string? observacao)
    {
        var rotulo = motivo switch
        {
            MotivoEntregaEpi.Inicial => "Inicial",
            MotivoEntregaEpi.Dano => "Dano",
            MotivoEntregaEpi.Extravio => "Extravio",
            MotivoEntregaEpi.Vencimento => "Vencimento",
            MotivoEntregaEpi.TrocaDeFuncao => "Troca de função",
            _ => "não informado",
        };
        return string.IsNullOrWhiteSpace(observacao) ? rotulo : $"{rotulo} ({observacao})";
    }
}
