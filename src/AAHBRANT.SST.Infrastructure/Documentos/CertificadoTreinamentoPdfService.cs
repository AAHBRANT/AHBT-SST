using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Treinamentos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Modelo de certificado aprovado pelo usuário em 19/10 (mockup
// scripts/mockups/certificado-modelo-nr_2026-10-19_v1.html): moldura vinho/ouro com cantos
// vinho/preto/bege, frente centralizada com o colaborador e a NR em destaque (número + título, via
// CatalogoNormasRegulamentadoras) e verso com o conteúdo programático em 2 colunas e a faixa de
// carga horária, data e local. Continua valendo: logo só a cadastrada na Obra (04/09), instrutor =
// responsável técnico numa assinatura só (06/09) e o rodapé padrão de rastreabilidade com QR.
public class CertificadoTreinamentoPdfService : ICertificadoTreinamentoPdfService
{
    private const string CorMarca = "#670000";
    private const string CorOuro = "#b89a6a";
    private const string CorTexto = "#1d1d1d";
    private const string CorTextoSuave = "#444444";
    private const string CorFaixa = "#f3f1ea";
    private const string CorPapel = "#fbfaf6";

    // A4 paisagem em pontos (842 x 595). Cantos pequenos o bastante para não encostar no QR do
    // rodapé, que fica no canto inferior direito da área útil.
    private const string MolduraSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="842" height="595" viewBox="0 0 842 595">
          <rect x="0" y="0" width="842" height="595" fill="#fbfaf6"/>
          <rect x="12" y="12" width="818" height="571" fill="none" stroke="#670000" stroke-width="2.5"/>
          <rect x="20" y="20" width="802" height="555" fill="none" stroke="#b89a6a" stroke-width="0.8"/>
          <polygon points="0,0 72,0 0,72" fill="#670000"/>
          <polygon points="80,0 94,0 0,94 0,80" fill="#1d1d1d"/>
          <polygon points="100,0 112,0 0,112 0,100" fill="#d8cdb6"/>
          <polygon points="842,595 770,595 842,523" fill="#670000"/>
          <polygon points="762,595 748,595 842,501 842,515" fill="#1d1d1d"/>
          <polygon points="742,595 730,595 842,483 842,495" fill="#d8cdb6"/>
        </svg>
        """;

    public byte[] Gerar(CertificadoTreinamentoPdfModelo modelo)
    {
        // Verso existe sempre que houver conteúdo programático (do curso ou o padrão da NR) ou foto
        // da turma para mostrar como evidência.
        var temVerso = !string.IsNullOrWhiteSpace(modelo.ConteudoProgramatico) || modelo.FotoTurma is not null;

        var documento = Document.Create(container =>
        {
            container.Page(pagina =>
            {
                ConfigurarPagina(pagina);
                pagina.Content().Element(c => Frente(c, modelo));
                Rodape(pagina, modelo);
            });

            if (temVerso)
            {
                container.Page(pagina =>
                {
                    ConfigurarPagina(pagina);
                    pagina.Content().Element(c => Verso(c, modelo));
                    Rodape(pagina, modelo);
                });
            }
        });

        return documento.GeneratePdf();
    }

    private static void ConfigurarPagina(PageDescriptor pagina)
    {
        pagina.Size(PageSizes.A4.Landscape());
        pagina.MarginHorizontal(2.8f, Unit.Centimetre);
        pagina.MarginTop(1.3f, Unit.Centimetre);
        pagina.MarginBottom(1.1f, Unit.Centimetre);
        pagina.PageColor(CorPapel);
        pagina.DefaultTextStyle(estilo => estilo.FontSize(10).FontColor(CorTexto));
        pagina.Background().Svg(MolduraSvg);
    }

    private static void Rodape(PageDescriptor pagina, CertificadoTreinamentoPdfModelo modelo)
    {
        pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
            coluna, "Certificado", modelo.NumeroCertificado, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, modelo.TemAssinatura));
    }

    // Única logo permitida é a cadastrada na própria Obra (04/09); sem logo, só reserva o espaço.
    private static void Logo(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        if (modelo.ObraLogoConteudo is not null)
            container.AlignCenter().Height(40).Width(190).Image(modelo.ObraLogoConteudo).FitArea();
        else
            container.Height(12);
    }

    private static void Ornamento(IContainer container, float largura)
    {
        container.AlignCenter().Width(largura).Row(linha =>
        {
            linha.RelativeItem().AlignMiddle().LineHorizontal(0.6f).LineColor(CorOuro);
            linha.AutoItem().PaddingHorizontal(5).Text("◆").FontSize(6).FontColor(CorOuro);
            linha.RelativeItem().AlignMiddle().LineHorizontal(0.6f).LineColor(CorOuro);
        });
    }

    private static void Frente(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.Column(coluna =>
        {
            coluna.Item().Row(linha =>
            {
                linha.RelativeItem();
                linha.RelativeItem(3).Element(c => Logo(c, modelo));
                linha.RelativeItem().AlignRight().AlignTop().Element(c =>
                {
                    if (!string.IsNullOrWhiteSpace(modelo.NumeroCertificado))
                        c.Background(CorMarca).PaddingVertical(3).PaddingHorizontal(6)
                            .Text($"Nº {modelo.NumeroCertificado}").FontSize(7.5f).SemiBold().FontColor(Colors.White);
                });
            });

            coluna.Item().PaddingTop(6).AlignCenter().Text("CERTIFICADO").FontSize(44).Bold().FontColor(CorMarca).LetterSpacing(0.04f);
            coluna.Item().AlignCenter().Width(420).Row(linha =>
            {
                linha.RelativeItem().AlignMiddle().LineHorizontal(0.6f).LineColor(CorOuro);
                linha.AutoItem().PaddingHorizontal(10).Text("DE TREINAMENTO").FontSize(16).Bold().LetterSpacing(0.3f);
                linha.RelativeItem().AlignMiddle().LineHorizontal(0.6f).LineColor(CorOuro);
            });
            coluna.Item().PaddingTop(2).AlignCenter().Text("SEGURANÇA E SAÚDE NO TRABALHO").FontSize(7.5f).LetterSpacing(0.45f).FontColor(CorTextoSuave);
            coluna.Item().PaddingVertical(12).Element(c => Ornamento(c, 170));

            coluna.Item().AlignCenter().Text("Certificamos que o colaborador").FontSize(12.5f);
            coluna.Item().PaddingTop(2).AlignCenter().Text(modelo.TrabalhadorNome).FontSize(30).Italic().SemiBold().FontColor(CorMarca);
            coluna.Item().AlignCenter().Width(440).LineHorizontal(0.6f).LineColor(CorOuro);
            coluna.Item().PaddingTop(6).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(11).FontColor(CorTextoSuave));
                t.Span($"Matrícula: {(string.IsNullOrWhiteSpace(modelo.TrabalhadorMatricula) ? "—" : modelo.TrabalhadorMatricula)}");
                t.Span("   |   ").FontColor(CorOuro);
                t.Span($"Função: {(string.IsNullOrWhiteSpace(modelo.TrabalhadorFuncaoNome) ? "—" : modelo.TrabalhadorFuncaoNome)}");
            });

            coluna.Item().PaddingTop(14).AlignCenter().Text("participou e concluiu o treinamento da").FontSize(12.5f);
            Norma(coluna, modelo);

            coluna.Item().PaddingTop(10).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(10.5f).FontColor(CorTextoSuave));
                t.Span("Carga horária: ");
                t.Span($"{modelo.CargaHorariaRealizada} horas").SemiBold().FontColor(CorMarca);
                t.Span("   ·   Realizado em ");
                t.Span($"{modelo.DataRealizacao:dd/MM/yyyy}").SemiBold().FontColor(CorMarca);
                t.Span("   ·   Válido até ");
                t.Span($"{modelo.DataValidade:dd/MM/yyyy}").SemiBold().FontColor(CorMarca);
            });

            coluna.Item().PaddingTop(6).AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8f).FontColor(CorTextoSuave));
                t.Span("Promovido pelo SESMT da Aahbrant Engenharia e Construções LTDA");
                if (!string.IsNullOrWhiteSpace(modelo.ObraCnpj)) t.Span($", CNPJ {modelo.ObraCnpj}");
                if (!string.IsNullOrWhiteSpace(modelo.ObraNome)) t.Span($" — Obra {modelo.ObraNome}");
                if (!string.IsNullOrWhiteSpace(modelo.ObraCidade)) t.Span($", {modelo.ObraCidade}-{modelo.ObraUf}");
                t.Span(".");
            });

            coluna.Item().ExtendVertical().AlignBottom().PaddingBottom(4).Element(c => Assinaturas(c, modelo));
        });
    }

    // Número da NR em destaque e o título por extenso logo abaixo. Curso sem NR (ou NR fora do
    // catálogo) cai no nome do curso, que já é o que identifica o treinamento.
    private static void Norma(ColumnDescriptor coluna, CertificadoTreinamentoPdfModelo modelo)
    {
        var numero = modelo.NormaNumero ?? modelo.NormaReferencia;
        var titulo = modelo.NormaTitulo;
        if (string.IsNullOrWhiteSpace(numero))
        {
            coluna.Item().PaddingTop(2).AlignCenter().Text(modelo.CursoNome).FontSize(20).Italic().SemiBold().FontColor(CorMarca);
            return;
        }

        coluna.Item().PaddingTop(2).AlignCenter().Text(numero).FontSize(28).Bold().FontColor(CorMarca).LetterSpacing(0.04f);
        var subtitulo = string.IsNullOrWhiteSpace(titulo) ? modelo.CursoNome : titulo;
        if (!string.IsNullOrWhiteSpace(subtitulo) && !string.Equals(subtitulo, numero, StringComparison.OrdinalIgnoreCase))
            coluna.Item().AlignCenter().Text(subtitulo).FontSize(19).Italic().SemiBold().FontColor(CorMarca);
    }

    private static void Verso(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        var norma = string.IsNullOrWhiteSpace(modelo.NormaNumero)
            ? modelo.NormaReferencia
            : string.IsNullOrWhiteSpace(modelo.NormaTitulo) ? modelo.NormaNumero : $"{modelo.NormaNumero} — {modelo.NormaTitulo}";

        // Decoration: a caixa do conteúdo ocupa o espaço entre o título e a faixa inferior, sem
        // empurrar a faixa para uma página a mais (ExtendVertical num Column fazia isso).
        container.Decoration(decoracao =>
        {
            decoracao.Before().Column(cabecalho => VersoCabecalho(cabecalho, modelo, norma));
            decoracao.Content().PaddingTop(10).ExtendVertical().Border(0.8f).BorderColor(CorOuro).CornerRadius(8)
                .PaddingVertical(12).PaddingHorizontal(18).Element(c => Topicos(c, modelo.ConteudoProgramatico));
            decoracao.After().PaddingTop(8).Element(c => VersoFaixa(c, modelo));
        });
    }

    private static void VersoCabecalho(ColumnDescriptor coluna, CertificadoTreinamentoPdfModelo modelo, string? norma)
    {
            coluna.Item().Element(c => Logo(c, modelo));
            coluna.Item().PaddingTop(6).AlignCenter().Text("CONTEÚDO PROGRAMÁTICO").FontSize(26).Bold().FontColor(CorMarca).LetterSpacing(0.03f);
            coluna.Item().PaddingVertical(6).Element(c => Ornamento(c, 300));
            coluna.Item().AlignCenter().Text(t =>
            {
                t.AlignCenter();
                t.DefaultTextStyle(x => x.FontSize(10).FontColor(CorTextoSuave));
                if (string.IsNullOrWhiteSpace(norma))
                {
                    t.Span("Conteúdo ministrado conforme os objetivos do treinamento ");
                    t.Span(modelo.CursoNome).SemiBold().FontColor(CorMarca);
                    t.Span(".");
                }
                else
                {
                    t.Span("Conteúdo ministrado conforme os requisitos da ");
                    t.Span(norma).SemiBold().FontColor(CorMarca);
                    t.Span(" e os objetivos do treinamento.");
                }
            });
    }

    private static void VersoFaixa(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
            container.Background(CorFaixa).CornerRadius(8).PaddingVertical(8).PaddingHorizontal(14).Row(linha =>
            {
                ItemFaixa(linha.RelativeItem(), "CARGA HORÁRIA", $"{modelo.CargaHorariaRealizada} horas");
                linha.ConstantItem(1).Background(CorOuro);
                ItemFaixa(linha.RelativeItem(), "DATA DE REALIZAÇÃO", modelo.DataRealizacao.ToString("dd/MM/yyyy"));
                linha.ConstantItem(1).Background(CorOuro);
                ItemFaixa(linha.RelativeItem(1.4f), "LOCAL DE REALIZAÇÃO", string.IsNullOrWhiteSpace(modelo.Local) ? modelo.ObraNome : modelo.Local);

                // Foto da turma (evidência do treinamento em grupo, ver SessaoTreinamento), quando há.
                if (modelo.FotoTurma is not null)
                {
                    linha.ConstantItem(1).Background(CorOuro);
                    linha.ConstantItem(70).PaddingLeft(10).Column(bloco =>
                    {
                        bloco.Item().Text("EVIDÊNCIA").FontSize(6.5f).SemiBold().FontColor(CorTextoSuave).LetterSpacing(0.08f);
                        bloco.Item().PaddingTop(2).Height(38).Width(56).Image(RecortarFotoTurma(modelo.FotoTurma)).FitArea();
                    });
                }
            });
    }

    private static void ItemFaixa(IContainer container, string rotulo, string valor)
    {
        container.PaddingHorizontal(10).Column(bloco =>
        {
            bloco.Item().Text(rotulo).FontSize(6.5f).SemiBold().FontColor(CorTextoSuave).LetterSpacing(0.08f);
            bloco.Item().PaddingTop(2).Text(valor).FontSize(12).SemiBold().FontColor(CorMarca);
        });
    }

    // Um tópico por linha (mesmo formato do cadastro do curso), em 2 colunas para caber listas
    // longas como as da NR-10.
    private static void Topicos(IContainer container, string? conteudo)
    {
        var topicos = (conteudo ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.TrimStart('•', '-', '*', ' '))
            .Where(t => t.Length > 0)
            .ToList();

        if (topicos.Count == 0)
        {
            container.AlignCenter().AlignMiddle().Text("Conteúdo programático não cadastrado para este curso.").FontSize(9).Italic().FontColor(CorTextoSuave);
            return;
        }

        // Lista curta (NR-35, NR-33…) fica numa coluna só, com letra maior, para preencher a caixa.
        if (topicos.Count <= 8)
        {
            Lista(container.PaddingHorizontal(10), topicos, 11.5f);
            return;
        }

        var longo = topicos.Sum(t => t.Length) > 900;
        var tamanho = longo ? 8.5f : 10f;
        var metade = (topicos.Count + 1) / 2;

        container.Row(linha =>
        {
            Lista(linha.RelativeItem(), topicos.Take(metade), tamanho);
            linha.ConstantItem(22);
            Lista(linha.RelativeItem(), topicos.Skip(metade), tamanho);
        });
    }

    private static void Lista(IContainer container, IEnumerable<string> topicos, float tamanho)
    {
        container.Column(lista =>
        {
            lista.Spacing(tamanho * 0.5f);
            foreach (var topico in topicos)
            {
                lista.Item().Row(item =>
                {
                    item.ConstantItem(12).PaddingTop(tamanho * 0.15f).Text("◆").FontSize(tamanho * 0.6f).FontColor(CorMarca);
                    item.RelativeItem().Text(topico).FontSize(tamanho).LineHeight(1.25f);
                });
            }
        });
    }

    private static byte[] RecortarFotoTurma(byte[] foto)
    {
        using var imagem = SixLabors.ImageSharp.Image.Load(foto);
        imagem.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(440, 300),
            Mode = ResizeMode.Crop,
        }));

        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder { Quality = 85 });
        return saida.ToArray();
    }

    private static void Assinaturas(IContainer container, CertificadoTreinamentoPdfModelo modelo)
    {
        container.ShowEntire().Row(linha =>
        {
            // O instrutor é sempre o Técnico de Segurança do Trabalho responsável, que também assina
            // como Responsável Técnico — uma assinatura só (pedido do usuário, 06/09).
            linha.RelativeItem().Column(bloco =>
            {
                bloco.Item().PaddingTop(14).AlignCenter().Text((modelo.InstituicaoInstrutor ?? "Instrutor responsável").ToUpperInvariant()).FontSize(9).SemiBold();
                bloco.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(CorTexto);
                bloco.Item().PaddingTop(2).AlignCenter().Text("Instrutor e Responsável Técnico pelo Treinamento").FontSize(7.5f).SemiBold();
                bloco.Item().AlignCenter().Text("Técnico de Segurança do Trabalho").FontSize(7).FontColor(CorTextoSuave);
                if (!string.IsNullOrWhiteSpace(modelo.InstrutorRegistroProfissional))
                    bloco.Item().AlignCenter().Text($"Registro: {modelo.InstrutorRegistroProfissional}").FontSize(7).FontColor(CorTextoSuave);
            });

            // Sem nenhuma assinatura ainda, mostra o trabalhador como "Aguardando".
            if (modelo.Signatarios.Count == 0)
            {
                linha.ConstantItem(40);
                linha.RelativeItem().Element(c => AssinaturaPdfPadrao.Bloco(c, null, modelo.TrabalhadorNome, "Participante", null, null));
            }

            foreach (var signatario in modelo.Signatarios)
            {
                linha.ConstantItem(40);
                linha.RelativeItem().Element(c => AssinaturaPdfPadrao.Bloco(
                    c, null, signatario.TrabalhadorNome,
                    string.IsNullOrWhiteSpace(signatario.Funcao) ? "Participante" : signatario.Funcao,
                    signatario.AssinadoEm, signatario.Metodo));
            }
        });
    }
}
