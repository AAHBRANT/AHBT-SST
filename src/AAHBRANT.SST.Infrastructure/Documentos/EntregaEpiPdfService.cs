using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Application.TermosCompromissoEpi;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using SixLabors.ImageSharp.Processing;
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
                    // Ordem do documento (pedido do usuário): 1 Identificação, 2 Controle de Entrega,
                    // 3 Controle de Devolução, 4 Termo de Recebimento (que fecha o documento com a
                    // assinatura do empregado). Sem a antiga seção "Observação" no fim.
                    coluna.Item().Element(c => SecaoControleEntrega(c, modelo));
                    coluna.Item().Element(c => SecaoControleDevolucao(c, modelo));
                    coluna.Item().Element(SecaoTermoCompromisso(modelo));
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ficha de EPI", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
            });

            // Páginas finais: log de assinaturas de EPI do funcionário (as páginas acima não mudam).
            if (modelo.Log is { Itens.Count: > 0 } log)
                LogAssinaturasEpiPdf.Adicionar(container, modelo, log);
        });

        return documento.GeneratePdf();
    }

    private static void SecaoIdentificacao(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("1. Identificação do Trabalhador").FontSize(11).Bold().FontColor(CorMarca);

            // Grade de 3 colunas, cada campo numa caixa com o rótulo pequeno em cima e o valor embaixo
            // (mesmo desenho do cabeçalho do DDS semanal) — linhas alinhadas e sem quebra irregular.
            // À direita, a foto de cadastro do funcionário (pedido de 06/10); o turno saiu da grade e o
            // CPF passou a sair completo (liberado pelo jurídico).
            coluna.Item().PaddingTop(2).Row(linha =>
            {
                linha.RelativeItem().Table(tabela =>
                {
                    tabela.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    CelulaIdentificacao(tabela, "Nome completo", modelo.TrabalhadorNome, colSpan: 2);
                    CelulaIdentificacao(tabela, "CPF", modelo.TrabalhadorCpf ?? modelo.TrabalhadorCpfMascarado);
                    CelulaIdentificacao(tabela, "Matrícula", modelo.TrabalhadorMatricula);
                    CelulaIdentificacao(tabela, "Função", modelo.TrabalhadorFuncaoNome);
                    CelulaIdentificacao(tabela, "Data de admissão", modelo.TrabalhadorDataAdmissao.ToString("dd/MM/yyyy"));
                    CelulaIdentificacao(tabela, "Obra / Frente de trabalho", modelo.ObraNome, colSpan: 3);
                    CelulaIdentificacao(tabela, "Empresa contratante", modelo.ObraNome, colSpan: 2);
                    CelulaIdentificacao(tabela, "CNPJ da contratada", modelo.ObraCnpj ?? "não informado");
                });

                // Foto solta no formato 3x4 (3 cm x 4 cm = 85 x 113 pt), sem moldura nem legenda (pedido de
                // 08/10). A imagem é recortada para 3:4 antes de entrar, assim preenche o quadro inteiro
                // sem faixas brancas e sem distorcer.
                linha.ConstantItem(FotoLarguraPt + 10).PaddingLeft(10).Height(FotoAlturaPt).Element(foto =>
                {
                    var recortada = modelo.TrabalhadorFoto is { Length: > 0 } bytes ? RecortarFoto3x4(bytes) : null;
                    if (recortada is not null)
                        foto.Image(recortada).FitArea();
                    else
                        foto.Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                            .AlignCenter().AlignMiddle().Text("Sem foto cadastrada").FontSize(7).FontColor(Colors.Grey.Darken1).AlignCenter();
                });
            });
        });
    }

    private const float FotoLarguraPt = 85f;   // 3 cm
    private const float FotoAlturaPt = 113.4f; // 4 cm

    // Recorta ao centro na proporção 3:4, puxando o corte para cima (rosto fica no terço superior).
    // Devolve null se os bytes não forem uma imagem válida.
    private static byte[]? RecortarFoto3x4(byte[] bytes)
    {
        try
        {
            using var imagem = SixLabors.ImageSharp.Image.Load(bytes);
            imagem.Mutate(x => x.AutoOrient());
            const double alvo = 3.0 / 4.0;
            int largura = imagem.Width, altura = imagem.Height;
            int cw = largura, ch = altura;
            if ((double)largura / altura > alvo) cw = (int)Math.Round(altura * alvo);
            else ch = (int)Math.Round(largura / alvo);
            var x0 = (largura - cw) / 2;
            var y0 = (int)Math.Round((altura - ch) * 0.25);
            imagem.Mutate(x => x.Crop(new SixLabors.ImageSharp.Rectangle(x0, y0, cw, ch)));
            using var saida = new MemoryStream();
            imagem.Save(saida, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });
            return saida.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static bool FotoValida(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            return SixLabors.ImageSharp.Image.Identify(ms) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void CelulaIdentificacao(TableDescriptor tabela, string rotulo, string valor, uint colSpan = 1)
    {
        tabela.Cell().ColumnSpan(colSpan).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(celula =>
        {
            celula.Item().Text(rotulo).FontSize(7).SemiBold().FontColor(CorMarca);
            celula.Item().Text(valor).FontSize(9);
        });
    }

    private static Action<IContainer> SecaoTermoCompromisso(FichaEpiPdfModelo modelo)
    {
        var contratante = modelo.ObraNome;
        // Data e nº vêm do certificado de NR-06 do trabalhador (pedido de 02/10); sem certificado,
        // ficam em branco para preenchimento à mão — a trava de NR-06 já barra a entrega nesse caso.
        var dataNr6 = modelo.DataTreinamentoNr6?.ToString("dd/MM/yyyy") ?? "____/____/______";
        var numeroNr6 = string.IsNullOrWhiteSpace(modelo.NumeroCertificadoNr6) ? "__________" : modelo.NumeroCertificadoNr6;

        return container => container.Column(coluna =>
        {
            coluna.Spacing(3);
            coluna.Item().Text("4. Termo de Recebimento e Compromisso de Uso").FontSize(11).Bold().FontColor(CorMarca);

            coluna.Item().Text($"1 — Declaro ter recebido do {contratante} os Equipamentos de Proteção Individual (EPIs) relacionados nesta ficha, nas datas e quantidades ali indicadas, todos em perfeitas condições de uso e com Certificado de Aprovação (CA) válido.");
            coluna.Item().Text($"2 — Declaro ter recebido orientação e treinamento sobre o uso correto, a guarda, a conservação, a higienização e os critérios de substituição de cada EPI relacionado, conforme registrado na Lista de Presença de Treinamento (NR-6) nº {numeroNr6}, realizada em {dataNr6}.");
            coluna.Item().Text("3 — Comprometo-me a utilizar os EPIs exclusivamente para a finalidade a que se destinam, durante toda a execução das minhas atividades laborais, zelando por sua guarda, conservação e higienização adequadas, e a comunicar imediatamente ao Setor de Segurança do Trabalho qualquer dano, extravio ou alteração que os torne impróprios para uso.");
            coluna.Item().Text("4 — Comprometo-me a devolver os EPIs sempre que solicitado, inclusive nos casos de substituição, troca de função, mudança de atividade ou rescisão do meu contrato de trabalho.");
            coluna.Item().Text("5 — Estou ciente de que o descumprimento das obrigações aqui assumidas constitui falta funcional, passível de sanções disciplinares que poderão variar, a critério do empregador, de advertência por escrito até a rescisão contratual por justa causa, sem prejuízo de demais medidas legais cabíveis, conforme disposto no Art. 158 da CLT e na Norma Regulamentadora nº 6 (NR-6).");

            // Assinatura do empregado no termo (03/10): digital (Motor), em papel (registro manual) ou
            // pendente. O papel NUNCA vira "Assinado digitalmente": só informa quem registrou e quando.
            coluna.Item().PaddingTop(10).AlignCenter().Width(300).Element(c => BlocoAssinaturaTermo(c, modelo));
        });
    }

    private static void BlocoAssinaturaTermo(IContainer container, FichaEpiPdfModelo modelo)
    {
        var termo = modelo.Termo;
        if (termo is { Situacao: SituacaoTermoCompromissoEpi.Manual })
        {
            container.ShowEntire().Column(coluna =>
            {
                coluna.Item().AlignCenter().Text("Empregado — Termo de Compromisso").FontSize(7.5f).Bold();
                coluna.Item().PaddingTop(10).AlignCenter().Text(modelo.TrabalhadorNome).FontSize(9).SemiBold();
                coluna.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Colors.Black);
                coluna.Item().PaddingTop(2).AlignCenter().Text(modelo.TrabalhadorFuncaoNome).FontSize(7).SemiBold();
                coluna.Item().AlignCenter()
                    .Text($"Termo assinado manualmente (em papel) em {termo.DataAssinatura:dd/MM/yyyy}")
                    .FontSize(AssinaturaPdfPadrao.TamanhoFonte).Italic().FontColor(AssinaturaPdfPadrao.Cor);
                if (termo.RegistradoEm is { } registradoEm)
                    coluna.Item().AlignCenter()
                        .Text($"Registrado no sistema por {termo.RegistradoPorNome ?? "usuário não identificado"} em {HorarioBrasilia.De(registradoEm):dd/MM/yyyy HH:mm}")
                        .FontSize(AssinaturaPdfPadrao.TamanhoFonte).FontColor(AssinaturaPdfPadrao.Cor);
            });
            return;
        }

        var digital = termo is { Situacao: SituacaoTermoCompromissoEpi.Digital };
        AssinaturaPdfPadrao.Bloco(
            container, "Empregado — Termo de Compromisso", modelo.TrabalhadorNome, modelo.TrabalhadorFuncaoNome,
            digital ? termo!.DataAssinatura : null, digital ? termo!.Metodo : null);
    }

    private static void SecaoControleEntrega(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("2. Controle de Entrega de EPI").FontSize(11).Bold().FontColor(CorMarca);

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
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(2.2f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CabecalhoCelula).Text("Nº");
                    header.Cell().Element(CabecalhoCelula).Text("EPI");
                    header.Cell().Element(CabecalhoCelula).Text("CA");
                    header.Cell().Element(CabecalhoCelula).Text("Observação");
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
                    table.Cell().Element(Celula).Element(c => CelulaAssinatura(c, linha.AssinadoPeloEmpregadoEm, linha.AssinadoPeloEmpregado, linha.MetodoEmpregado));
                    table.Cell().Element(Celula).Element(c => CelulaAssinatura(c, linha.AssinadoPeloResponsavelEm, linha.AssinadoPeloResponsavel, linha.MetodoResponsavel));
                }
            });
        });
    }

    private static void SecaoControleDevolucao(IContainer container, FichaEpiPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Text("3. Controle de Devolução de EPI").FontSize(11).Bold().FontColor(CorMarca);

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
                    columns.RelativeColumn(2.2f);
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
                    table.Cell().Element(Celula).Element(c => CelulaAssinatura(c, linha.AssinadoPeloEmpregadoEm, linha.AssinadoPeloEmpregado, linha.MetodoEmpregado));
                    table.Cell().Element(Celula).Text(linha.VistoResponsavel ?? "-");
                }
            });
        });
    }

    private static IContainer CabecalhoCelula(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(3).BorderBottom(1).BorderColor(CorMarca).DefaultTextStyle(t => t.FontSize(7).Bold());

    private static IContainer Celula(IContainer container) =>
        container.Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).DefaultTextStyle(t => t.FontSize(7.5f));

    // "Assinado digitalmente" + legenda miúda (corpo 6) com data/hora e método de identificação —
    // pedido do usuário (02/10) para toda assinatura de documento; corpo pequeno para não esticar a linha.
    private static void CelulaAssinatura(IContainer container, DateTime? assinadoEm, bool assinado, MetodoAutenticacaoAssinatura? metodo)
    {
        if (!assinado)
        {
            container.Text("Pendente");
            return;
        }

        container.Column(c =>
        {
            c.Item().Text("Assinado digitalmente").FontSize(6);
            if (assinadoEm is { } quando)
                c.Item().Text(DescricaoMetodoAssinatura.LegendaCurta(HorarioBrasilia.De(quando), metodo))
                    .FontSize(6).FontColor(Colors.Grey.Darken2);
        });
    }

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
