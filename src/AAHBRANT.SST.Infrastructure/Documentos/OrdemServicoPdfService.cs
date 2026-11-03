using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// PDF da Ordem de Serviço de SST emitida (modelo aprovado em 03/11, v6 sobre o v5): cabeçalho padrão
// com a logo da obra, as nove seções da minuta e as assinaturas do funcionário e do técnico de segurança.
public class OrdemServicoPdfService : IOrdemServicoPdfService
{
    private const string CorMarca = "#670000";
    private const string Bege = "#ebe9ad";

    private static readonly string[] Responsabilidades =
    {
        "Cumprir as disposições legais e regulamentares de SST e as instruções desta OS (NR-01, item 1.4.2).",
        "Utilizar máquinas, ferramentas e equipamentos somente para os quais esteja capacitado e autorizado, obedecendo aos procedimentos e normas da empresa.",
        "Usar os EPIs fornecidos, guardá-los, conservá-los e comunicar qualquer alteração que os torne impróprios (NR-06).",
        "Comunicar prontamente acidentes, incidentes, quase acidentes, condições inseguras, falhas de equipamentos e mudanças nas condições de trabalho.",
        "Participar das orientações e capacitações aplicáveis e colaborar com as medidas de prevenção.",
        "Exercer o direito de recusa diante de risco grave e iminente, comunicando imediatamente a liderança (NR-01, item 1.4.3).",
    };

    public byte[] Gerar(OrdemServicoPdfModelo modelo)
    {
        var c = modelo.Conteudo;
        var situacao = modelo.Situacao == SituacaoOrdemServico.Valida ? "VÁLIDA" : "AGUARDANDO ASSINATURA";
        var cabecalhoDireita = new List<string>
        {
            $"Nº {modelo.Numero}",
            $"Revisão {modelo.Revisao}",
            $"Emissão {HorarioBrasilia.De(modelo.EmitidaEm):dd/MM/yyyy}",
            $"Situação: {situacao}",
        };

        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(8.5f));

                pagina.Header().Column(coluna =>
                {
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Ordem de Serviço de Segurança e Saúde no Trabalho", c.Obra.Nome,
                        modelo.ObraLogoConteudo, cabecalhoDireita);
                    coluna.Item().AlignCenter().Text("NR-01, item 1.4.1, alínea \"c\"  •  CLT, art. 157, inciso II").FontSize(7.5f);
                });

                pagina.Content().PaddingVertical(6).Column(col =>
                {
                    col.Spacing(2);

                    Secao(col, 1, "Identificação da empresa e da obra");
                    Linha(col, "Obra / Contrato", c.Obra.Nome);
                    Linha(col, "Cliente", c.Obra.Cliente);
                    Linha(col, "CNPJ", c.Obra.Cnpj);
                    Linha(col, "Endereço da obra", c.Obra.Endereco);

                    Secao(col, 2, "Identificação do trabalhador");
                    Linha(col, "Nome", c.Trabalhador.Nome);
                    Linha(col, "Matrícula / CPF", $"{c.Trabalhador.Matricula ?? "—"}  •  CPF {c.Trabalhador.CpfMascarado}");
                    Linha(col, "Função / CBO", $"{c.Trabalhador.Funcao}{(c.Trabalhador.Cbo is null ? "" : $"  •  CBO {c.Trabalhador.Cbo}")}");
                    Linha(col, "Admissão", c.Trabalhador.DataAdmissao.ToString("dd/MM/yyyy"));

                    Secao(col, 3, "Atividades da função (conforme PGR)");
                    if (c.Pgr is not null)
                        col.Item().Text($"Fonte: {c.Pgr.Nome}{(c.Pgr.Revisao is null ? "" : $" — revisão {c.Pgr.Revisao}")}").FontSize(7.5f).Italic();
                    foreach (var g in c.Ghes)
                        col.Item().Text(t =>
                        {
                            t.Span($"GHE {g.Numero:00}: ").SemiBold();
                            t.Span(string.IsNullOrWhiteSpace(g.DescricaoAtividades) ? "—" : g.DescricaoAtividades);
                        });
                    col.Item().Text("Esta OS não amplia as atividades acima. Atividades fora desta lista não estão autorizadas por este documento.").FontSize(7.5f);

                    Secao(col, 4, "Atividades da função: riscos, prevenção e requisitos");
                    foreach (var a in c.Atividades)
                        col.Item().PaddingTop(4).Element(e => Atividade(e, a));

                    Secao(col, 5, "EPIs obrigatórios");
                    Lista(col, c.Epis.Select(e => e.Ca is null ? e.Nome : $"{e.Nome} — CA {e.Ca}"));

                    Secao(col, 6, "Qualificações e autorizações do trabalhador");
                    var qualificacoes = c.Treinamentos.Select(t => $"{t.Curso}: " + t.Situacao switch
                    {
                        "Valido" => $"válido até {Data(t.Validade)}",
                        "Vencido" => $"vencido em {Data(t.Validade)}",
                        _ => "não realizado",
                    }).ToList();
                    qualificacoes.Add(c.Aso is null ? "ASO: não cadastrado"
                        : $"ASO: {(c.Aso.Valido ? $"apto até {Data(c.Aso.Validade)}" : "vencido ou sem aptidão")}");
                    Lista(col, qualificacoes);

                    Secao(col, 7, "Responsabilidades do trabalhador");
                    Lista(col, Responsabilidades);

                    Secao(col, 8, "Políticas corporativas aplicáveis");
                    Lista(col, c.Politicas.Select(p =>
                        $"{(p.Codigo is null ? "" : $"{p.Codigo} — ")}{p.Titulo} (rev. {p.Revisao ?? "—"}){(p.FormaConsulta is null ? "" : $" • Consulta: {p.FormaConsulta}")}"));
                    col.Item().Text("As políticas complementam as instruções detalhadas desta OS e não as substituem.").FontSize(7.5f);

                    Secao(col, 9, "Ciência e responsabilidade");
                    col.Item().Text(
                        "Declaro ter recebido esta Ordem de Serviço, ter sido informado(a) de forma clara sobre as atividades, os riscos, " +
                        "as medidas de prevenção e os limites da minha função, e comprometo-me a cumpri-la. Estou ciente de que a recusa " +
                        "injustificada em observar as instruções de segurança e em usar os EPIs constitui ato faltoso (CLT, art. 158, parágrafo único).")
                        .Justify();

                    col.Item().PaddingTop(18).ShowEntire().Row(linha =>
                    {
                        linha.Spacing(24);
                        linha.RelativeItem().Element(e => AssinaturaPdfPadrao.Bloco(e, "TRABALHADOR(A)",
                            modelo.Funcionario?.Nome ?? c.Trabalhador.Nome, c.Trabalhador.Funcao,
                            modelo.Funcionario?.AssinadoEm, modelo.Funcionario?.Metodo));
                        linha.RelativeItem().Element(e => AssinaturaPdfPadrao.Bloco(e, "TÉCNICO DE SEGURANÇA DO TRABALHO",
                            modelo.Tecnico?.Nome, modelo.Tecnico?.Funcao,
                            modelo.Tecnico?.AssinadoEm, modelo.Tecnico?.Metodo));
                    });

                    col.Item().PaddingTop(8).Text("Revisar a cada mudança de função, atividade, risco ou revisão do PGR.").FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
                    coluna, "Ordem de Serviço", modelo.Numero, modelo.Revisao, modelo.ConteudoHash, modelo.UrlValidacaoPublica,
                    modelo.QrCodePng, modelo.Funcionario is not null || modelo.Tecnico is not null));
            });
        }).GeneratePdf();
    }

    private static void Atividade(IContainer container, AtividadeOsDto a)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background(Bege).Padding(4).Text(a.Nome).SemiBold();
            col.Item().Padding(4).Column(corpo =>
            {
                corpo.Spacing(2);
                Campo(corpo, "Condições", a.Condicoes);
                foreach (var r in a.Riscos)
                {
                    corpo.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span($"[{Nivel(r.Nivel)}] ").SemiBold().FontColor(CorMarca);
                        t.Span(r.Perigo).SemiBold();
                        if (!string.IsNullOrWhiteSpace(r.Consequencia)) t.Span($" — {r.Consequencia}");
                    });
                    Fase(corpo, "Antes do trabalho", r.Antes);
                    Fase(corpo, "Durante o trabalho", r.Durante);
                    Fase(corpo, "Após o trabalho", r.Apos);
                }
                Campo(corpo, "Ferramentas e equipamentos", Juntar(a.Ferramentas));
                Campo(corpo, "EPCs", Juntar(a.Epcs));
                Campo(corpo, "Procedimentos aplicáveis", Juntar(a.Procedimentos, "; "));
                Campo(corpo, "Interromper a atividade e comunicar a liderança quando", Juntar(a.InterromperQuando, "; "));
                Campo(corpo, "Não executar sem capacitação, autorização ou liberação",
                    Juntar(a.Restricoes.Select(r => r.Requisito is null ? r.Texto : $"{r.Texto} → {r.Requisito}"), "; "));
                Campo(corpo, "Autorizações exigidas", Juntar(a.Autorizacoes, "; "));
            });
        });
    }

    private static void Secao(ColumnDescriptor col, int numero, string titulo) =>
        col.Item().PaddingTop(8).Background(CorMarca).PaddingVertical(3).PaddingHorizontal(6)
            .Text($"{numero}. {titulo.ToUpperInvariant()}").FontSize(8.5f).Bold().FontColor(Colors.White);

    private static void Linha(ColumnDescriptor col, string rotulo, string? valor) =>
        col.Item().Row(linha =>
        {
            linha.ConstantItem(130).Background("#f4f4f4").Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(rotulo).SemiBold();
            linha.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor);
        });

    private static void Campo(ColumnDescriptor col, string rotulo, string? valor) =>
        col.Item().Text(t =>
        {
            t.Span($"{rotulo}: ").SemiBold();
            t.Span(string.IsNullOrWhiteSpace(valor) ? "—" : valor);
        });

    private static void Fase(ColumnDescriptor col, string fase, List<string> medidas)
    {
        if (medidas.Count == 0) return;
        col.Item().PaddingLeft(8).Text(t =>
        {
            t.Span($"{fase}: ").SemiBold().FontColor(CorMarca);
            t.Span(string.Join("; ", medidas));
        });
    }

    private static void Lista(ColumnDescriptor col, IEnumerable<string> itens)
    {
        var lista = itens.ToList();
        if (lista.Count == 0)
        {
            col.Item().Text("—");
            return;
        }
        foreach (var item in lista)
            col.Item().PaddingLeft(6).Text($"• {item}").Justify();
    }

    private static string Juntar(IEnumerable<string> itens, string separador = ", ")
    {
        var lista = itens.ToList();
        return lista.Count == 0 ? "—" : string.Join(separador, lista);
    }

    private static string Data(DateTime? data) => data?.ToString("dd/MM/yyyy") ?? "—";

    private static string Nivel(NivelRisco nivel) => nivel switch
    {
        NivelRisco.Trivial => "Trivial",
        NivelRisco.Baixo => "Baixo",
        NivelRisco.Moderado => "Moderado",
        NivelRisco.Alto => "Alto",
        NivelRisco.Critico => "Crítico",
        _ => nivel.ToString(),
    };
}
