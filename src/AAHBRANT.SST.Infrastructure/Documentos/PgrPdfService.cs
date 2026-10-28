using System.Globalization;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// PDF do PGR gerado pela EAP (23/10/2026), na estrutura do modelo de PGR enviado pelo usuário (capa,
// sumário, informações gerais, introdução, objetivos, responsabilidades, matriz de risco, GHE com fichas de
// risco, plano de ação, acompanhamento, emergência e assinaturas) e em design minimalista: fundo branco,
// texto em cinza, linhas finas e o vinho da marca só como detalhe. Os textos fixos são redação institucional
// própria; só riscos não excluídos e medidas aceitas entram no documento.
public class PgrPdfService : IPgrPdfService
{
    private const string Marca = "#670000";
    private const string Texto = "#2b2b2b";
    private const string Secundario = "#777777";
    private const string Linha = "#d9d9d9";
    private const string Fundo = "#f6f6f6";

    // Marca d'água da logo da obra (modelo A aprovado em 27/10/2026): centralizada (largura e reforço em MarcaDaguaLogo).
    private const float OpacidadeMarcaCapa = 0.085f;
    private const float OpacidadeMarcaPaginas = 0.07f;

    private sealed record Secao(string Id, string Numero, string Titulo);

    private static readonly Secao[] Secoes =
    {
        new("s1", "1", "Informações gerais"),
        new("s2", "2", "Introdução"),
        new("s3", "3", "Objetivos do PGR"),
        new("s4", "4", "Responsabilidades"),
        new("s5", "5", "Etapas do PGR e matriz de risco"),
        new("s6", "6", "Grupos homogêneos de exposição (GHE)"),
        new("s7", "7", "Plano de ação"),
        new("s8", "8", "Acompanhamento e monitoramento"),
        new("s9", "9", "Prioridades e avaliação do PGR"),
        new("s10", "10", "Plano de emergência"),
        new("s11", "11", "Considerações finais"),
    };

    public byte[] Gerar(DadosPgrPdf d, byte[]? logoObra)
    {
        var emitidoEm = HorarioBrasilia.De(d.GeradoEmUtc);
        // Marca d'água com a logo cadastrada na Obra em todas as páginas; um pouco mais forte na capa.
        var marcaCapa = MarcaDaguaLogo.Preparar(logoObra, OpacidadeMarcaCapa);
        var marcaPaginas = MarcaDaguaLogo.Preparar(logoObra, OpacidadeMarcaPaginas);

        return Document.Create(container =>
        {
            Capa(container, d, marcaCapa);
            Sumario(container, d, marcaPaginas);
            Conteudo(container, d, emitidoEm, marcaPaginas);
        }).GeneratePdf();
    }

    // ───────────────────────────── Capa e sumário ─────────────────────────────

    private static void Capa(IDocumentContainer container, DadosPgrPdf d, MarcaDagua? marcaDagua)
    {
        container.Page(p =>
        {
            ConfigurarPagina(p, marcaDagua);
            p.Content().Column(c =>
            {
                c.Item().Height(60);
                c.Item().PaddingTop(150).Text("PROGRAMA DE GERENCIAMENTO DE RISCOS").FontSize(9).LetterSpacing(0.12f).FontColor(Secundario);
                c.Item().PaddingTop(10).Text("PGR").FontSize(54).Bold().FontColor(Texto);
                c.Item().PaddingTop(10).Width(48).LineHorizontal(2).LineColor(Marca);
                c.Item().PaddingTop(18).Text(d.Nome).FontSize(18).SemiBold().FontColor(Texto);
                c.Item().PaddingTop(4).Text(d.ObraNome).FontSize(12).FontColor(Secundario);
                if (!string.IsNullOrWhiteSpace(d.ObraEndereco))
                    c.Item().PaddingTop(2).Text(d.ObraEndereco).FontSize(10).FontColor(Secundario);
                var local = string.Join(" / ", new[] { d.ObraCidade, d.ObraUf }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (local.Length > 0)
                    c.Item().PaddingTop(2).Text(local).FontSize(10).FontColor(Secundario);
                c.Item().PaddingTop(2).Text($"Vigência {d.DataElaboracao:yyyy}").FontSize(10).FontColor(Secundario);
            });
            p.Footer().Text($"AAHBRANT SST · elaborado em {d.DataElaboracao:dd/MM/yyyy}").FontSize(8).FontColor(Secundario);
        });
    }

    private static void Sumario(IDocumentContainer container, DadosPgrPdf d, MarcaDagua? marcaDagua)
    {
        container.Page(p =>
        {
            ConfigurarPagina(p, marcaDagua);
            Cabecalho(p, d);
            p.Content().Column(c =>
            {
                c.Item().PaddingBottom(14).Text("Sumário").FontSize(20).Bold().FontColor(Texto);
                foreach (var s in Secoes)
                {
                    c.Item().SectionLink(s.Id).PaddingVertical(5).BorderBottom(0.5f).BorderColor(Linha).Row(r =>
                    {
                        r.ConstantItem(28).Text(s.Numero).FontColor(Marca).SemiBold();
                        r.RelativeItem().Text(s.Titulo);
                        r.ConstantItem(30).AlignRight().Text(t => t.BeginPageNumberOfSection(s.Id).FontColor(Secundario));
                    });
                }
            });
            Rodape(p, d);
        });
    }

    // ───────────────────────────── Conteúdo ─────────────────────────────

    private static void Conteudo(IDocumentContainer container, DadosPgrPdf d, DateTime emitidoEm, MarcaDagua? marcaDagua)
    {
        container.Page(p =>
        {
            ConfigurarPagina(p, marcaDagua);
            Cabecalho(p, d);
            p.Content().Column(c =>
            {
                c.Spacing(7);
                var ghes = d.Estrutura.Ghes.OrderBy(g => g.Numero).ToList();
                var comp = d.Estrutura.Complementares ?? new DadosComplementaresPgr();

                // 1. Informações gerais
                Titulo(c, Secoes[0]);
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                    Campo(t, "Empresa / unidade", d.ObraNome);
                    Campo(t, "CNPJ", d.ObraCnpj);
                    Campo(t, "Endereço", d.ObraEndereco);
                    Campo(t, "CEP", comp.Cep);
                    Campo(t, "CNAE", comp.Cnae);
                    Campo(t, "Grau de risco (NR-04)", comp.GrauRisco?.ToString());
                    Campo(t, "Revisão atual", (d.Revisoes is { Count: > 0 } revisoes ? revisoes.Max(r => r.Numero) : 0).ToString("00"));
                    Campo(t, "Início da vigência", d.DataElaboracao.ToString("dd/MM/yyyy"));
                    Campo(t, "Revisão prevista", d.DataProximaRevisao?.ToString("dd/MM/yyyy"));
                    Campo(t, "Término da vigência", d.Estrutura.DataTermino?.ToString("dd/MM/yyyy"));
                    Campo(t, "Responsável técnico", d.AprovadoPorNome);
                    Campo(t, "Registro profissional (CREA)", comp.ResponsavelTecnicoRegistro);
                    Campo(t, "Elaborado por", d.ElaboradoPorNome);
                });
                Subtitulo(c, "Controle de revisões");
                Tabela(c, new[] { ("Revisão", 0.9f), ("Data", 1.2f), ("Descrição da alteração", 4f), ("Responsável", 2f) },
                    (d.Revisoes ?? new List<RevisaoPdf>()).Select(r => new[] { r.Numero.ToString("00"), r.Data.ToString("dd/MM/yyyy"), r.Motivo, r.Responsavel ?? "—" }),
                    "Sem revisões registradas.");

                // 2. Introdução
                TituloComPrimeiro(c, Secoes[1], b => Paragrafo(b, $"A {d.ObraNome} mantém como política conhecer os perigos e riscos presentes nas suas atividades e ambientes de trabalho e buscar, de forma contínua, a eliminação, a redução e o controle desses riscos, preservando a integridade física e a saúde dos trabalhadores."));
                Paragrafo(c, "Este Programa de Gerenciamento de Riscos (PGR) é específico para as atividades da obra e foi elaborado a partir da estrutura analítica do projeto (EAP). Cada atividade foi analisada para antecipar os perigos, avaliar os riscos e definir as medidas de prevenção.");

                // 3. Objetivos
                TituloComPrimeiro(c, Secoes[2], b => Paragrafo(b, "Formalizar o compromisso da organização com a programação das ações de preservação da integridade física e da saúde dos trabalhadores nas atividades da obra, com o inventário de riscos, as medidas de prevenção e o plano de ação."));

                // 4. Responsabilidades
                TituloComPrimeiro(c, Secoes[3], b => Subtitulo(b, "Da organização"));
                Lista(c, new[]
                {
                    "Assegurar o cumprimento da programação estabelecida neste PGR.",
                    "Priorizar a eliminação ou a redução dos perigos e garantir a proteção dos trabalhadores quando a eliminação total não for possível.",
                    "Informar os trabalhadores sobre os perigos de suas tarefas e as medidas que devem adotar.",
                    "Garantir o direito de interromper a atividade diante de risco grave e iminente.",
                    "Promover treinamentos de capacitação e manter o PCMSO em consonância com este PGR.",
                    "Implementar as medidas corretivas e revisar o PGR sempre que necessário.",
                });
                Subtitulo(c, "Dos trabalhadores");
                Lista(c, new[]
                {
                    "Cumprir as normas de segurança e participar das medidas de proteção deste PGR.",
                    "Comunicar situações ou atitudes que possam representar risco à saúde e à segurança.",
                    "Interromper a atividade e seguir as orientações da organização diante de risco grave e iminente.",
                    "Participar dos treinamentos e não executar atividade para a qual não tenha sido capacitado e autorizado.",
                });

                // 5. Etapas e matriz de risco
                TituloComPrimeiro(c, Secoes[4], b => Subtitulo(b, "Antecipação e identificação de riscos"));
                Paragrafo(c, "Antes do início dos serviços, os perigos de cada atividade da EAP são antecipados e as providências de proteção são definidas. O risco é a exposição ao perigo e resulta do cruzamento entre probabilidade e severidade na matriz abaixo.");
                Subtitulo(c, "Matriz de risco");
                MatrizRisco(c, d.Matriz);
                Subtitulo(c, "Zonas de decisão");
                Tabela(c, new[] { ("Nível de risco", 1.4f), ("Conduta", 4f) }, new[]
                {
                    new[] { "Trivial e baixo", "Manter os controles existentes." },
                    new[] { "Moderado", "Avaliar a necessidade de novos controles." },
                    new[] { "Alto", "Implementar novos controles." },
                    new[] { "Crítico", "Paralisar a atividade." },
                }, "");

                // 6. GHE
                Titulo(c, Secoes[5]);
                // Uma linha por função, com CBO e expostos do cadastro. Sem esses dados (PDF montado sem cadastro),
                // cai nas funções do próprio rascunho.
                var linhasFuncao = d.Funcoes is { Count: > 0 }
                    ? d.Funcoes
                    : ghes.SelectMany(g => g.Funcoes.Select(f => new LinhaFuncaoPdf(g.Numero, f.Nome, null, 0))).ToList();
                var jornadaPorGhe = ghes.ToDictionary(g => g.Numero, g => g.Jornada);
                var setorPorGhe = ghes.ToDictionary(g => g.Numero, g => g.Nome);
                Tabela(c, new[] { ("GHE", 0.7f), ("Setor", 2.2f), ("Função", 2.4f), ("CBO", 1f), ("Expostos", 1f), ("Jornada", 1.4f) },
                    linhasFuncao.OrderBy(f => f.GheNumero).ThenBy(f => f.Nome).Select(f => new[]
                    {
                        f.GheNumero.ToString("00"), setorPorGhe.GetValueOrDefault(f.GheNumero) ?? "—", f.Nome, f.Cbo ?? "—",
                        f.Expostos.ToString(), jornadaPorGhe.GetValueOrDefault(f.GheNumero) ?? "—",
                    }),
                    "Nenhum GHE definido.");

                foreach (var ghe in ghes)
                {
                    c.Item().PaddingTop(10).Row(r =>
                    {
                        r.ConstantItem(46).Text($"GHE {ghe.Numero:00}").FontColor(Marca).Bold();
                        r.RelativeItem().Text(ghe.Nome).FontSize(12).SemiBold();
                    });
                    if (!string.IsNullOrWhiteSpace(ghe.Ambiente))
                        c.Item().Text($"Ambiente: {ghe.Ambiente}").FontSize(8.5f).FontColor(Secundario);
                    var criticas = ghe.Atividades
                        .Where(a => a.Riscos.Any(r => !r.Excluido && NivelDe(d.Matriz, r) is NivelRisco.Alto or NivelRisco.Critico))
                        .Select(a => a.Nome).ToList();
                    if (criticas.Count > 0)
                        c.Item().Text($"Atividades críticas: {string.Join("; ", criticas)}").FontSize(8.5f).FontColor(Secundario);

                    foreach (var f in ghe.Funcoes)
                    {
                        var dados = linhasFuncao.FirstOrDefault(x => x.GheNumero == ghe.Numero && x.Nome == f.Nome);
                        c.Item().ShowEntire().Background(Fundo).Padding(7).Column(fc =>
                        {
                            fc.Item().Row(fr =>
                            {
                                fr.RelativeItem().Text(f.Nome).SemiBold();
                                fr.ConstantItem(170).AlignRight().Text($"CBO {dados?.Cbo ?? "—"} · {dados?.Expostos ?? 0} exposto(s)")
                                    .FontSize(8).FontColor(Secundario);
                            });
                            fc.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(f.Descricao) ? "Descrição das atividades não informada." : f.Descricao)
                                .FontSize(8.5f).FontColor(Secundario);
                        });
                    }

                    foreach (var atividade in ghe.Atividades)
                        foreach (var risco in atividade.Riscos.Where(r => !r.Excluido))
                            FichaRisco(c, d.Matriz, atividade, risco, d.Epis);
                }

                // 7. Plano de ação
                Titulo(c, Secoes[6]);
                var acoes = d.Estrutura.AcoesPlano
                    .Concat(d.Estrutura.PlanoAcao.Select(t => new AcaoPlanoGerada { Descricao = t }))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Descricao)).ToList();
                if (acoes.Count == 0)
                    c.Item().Text("Nenhuma ação registrada.").Italic().FontColor(Secundario);
                else
                {
                    Tabela(c, new[] { ("Nº", 0.5f), ("Ação", 3.8f), ("Responsável", 1.8f), ("Início", 1.45f), ("Prazo", 1.45f), ("Situação", 1.25f) },
                        acoes.Select((a, i) => new[]
                        {
                            (i + 1).ToString("00"), a.Descricao.Trim(), a.ResponsavelNome ?? "A definir",
                            a.Inicio?.ToString("dd/MM/yyyy") ?? "—", a.Prazo?.ToString("dd/MM/yyyy") ?? "A definir", "Pendente",
                        }),
                        "");
                    Cronograma(c, acoes);
                }

                // 7.1 Cronograma de treinamentos (NR-01, item 1.7): cursos obrigatórios por função, tirados do próprio PGR.
                Subtitulo(c, "7.1 Cronograma de treinamentos");
                Paragrafo(c, "Treinamentos obrigatórios por função, conforme os perigos e as atividades deste PGR e o item 1.7 da NR-01. Os cursos devem estar concluídos e válidos antes de a função iniciar a atividade que os exige.");
                var treinamentos = d.Treinamentos ?? Array.Empty<LinhaTreinamentoPdf>();
                if (treinamentos.Count == 0)
                    c.Item().Text("Nenhum curso obrigatório registrado para as funções deste PGR.").Italic().FontColor(Secundario);
                else
                {
                    foreach (var grupo in treinamentos.GroupBy(t => t.GheNumero).OrderBy(g => g.Key))
                    {
                        var nomeGhe = d.Estrutura.Ghes.FirstOrDefault(g => g.Numero == grupo.Key)?.Nome;
                        Subtitulo(c, $"GHE {grupo.Key:00}" + (string.IsNullOrWhiteSpace(nomeGhe) ? "" : $" · {nomeGhe}"));
                        Tabela(c, new[] { ("Função", 1.5f), ("Curso", 2.3f), ("Carga horária", 1.0f), ("Validade", 1.0f), ("Motivo (PGR)", 2.7f), ("Situação", 1.6f) },
                            grupo.Select(t => new[]
                            {
                                t.Funcao, t.Curso,
                                t.CargaHorariaMinima > 0 ? $"{t.CargaHorariaMinima} h" : "—",
                                t.ValidadeEmMeses > 0 ? $"{t.ValidadeEmMeses} meses" : "—",
                                string.IsNullOrWhiteSpace(t.Motivo) ? "—" : t.Motivo!,
                                t.Aprovado ? "Confirmado" : "A confirmar",
                            }),
                            "");
                    }
                    c.Item().PaddingTop(4).Text("Cursos \"a confirmar\" são sugestões do sistema e valem como previsão até a validação do Engenheiro de Segurança.")
                        .FontSize(8.5f).Italic().FontColor(Secundario);
                }

                // 7.2 Acompanhamento e aferição de resultados (NR-01, item 1.5.5.2): como cada ação do plano será
                // acompanhada, com que frequência e o que mostra que funcionou. Numeração igual à da tabela da seção 7.
                if (acoes.Count > 0)
                {
                    Subtitulo(c, "7.2 Acompanhamento e aferição de resultados");
                    Paragrafo(c, "Forma de acompanhamento de cada ação do plano de ação, com a frequência e o critério usado para verificar o resultado (NR-01, item 1.5.5.2).");
                    Tabela(c, new[] { ("Nº", 0.5f), ("Ação", 2.9f), ("Forma de acompanhamento", 1.8f), ("Periodicidade", 1.7f), ("Como aferir o resultado", 3.0f) },
                        acoes.Select((a, i) => new[]
                        {
                            (i + 1).ToString("00"), a.Descricao.Trim(),
                            string.IsNullOrWhiteSpace(a.FormaAcompanhamento) ? "A definir" : a.FormaAcompanhamento!,
                            string.IsNullOrWhiteSpace(a.PeriodicidadeAcompanhamento) ? "A definir" : a.PeriodicidadeAcompanhamento!,
                            string.IsNullOrWhiteSpace(a.CriterioAfericao) ? "A definir" : a.CriterioAfericao!.Trim(),
                        }),
                        "");
                    c.Item().PaddingTop(4).Text("Ações sem acompanhamento definido aparecem como \"A definir\".")
                        .FontSize(8.5f).Italic().FontColor(Secundario);
                }

                // 8. Acompanhamento e monitoramento
                TituloComPrimeiro(c, Secoes[7], b => Paragrafo(b, "A eficácia das medidas de controle será acompanhada por novas avaliações e inspeções, pelo controle estatístico das ocorrências de acidentes e pela análise dos exames médicos previstos no PCMSO."));
                Paragrafo(c, "Para os perigos geradores de riscos de acidentes serão programadas inspeções periódicas. Serão feitas avaliações extraordinárias sempre que houver mudança significativa nas condições de trabalho, alteração nos resultados dos exames médicos ou acidente com perda de tempo.");

                // 9. Prioridades e avaliação
                TituloComPrimeiro(c, Secoes[8], b => Paragrafo(b, "As medidas de proteção serão definidas, sempre que viável tecnicamente, nesta ordem de prioridade:"));
                Lista(c, new[]
                {
                    "eliminação dos perigos;",
                    "medidas de proteção coletiva;",
                    "medidas administrativas e procedimentos operacionais;",
                    "equipamentos de proteção individual.",
                });
                Paragrafo(c, "Este PGR será avaliado e revisado anualmente e a qualquer momento em que fatos justifiquem a revisão.");

                // 10. Plano de emergência
                TituloComPrimeiro(c, Secoes[9], b => Paragrafo(b, "A organização manterá um Plano de Emergência que oriente os trabalhadores sobre como agir em acidentes e anormalidades. Trabalhadores devem ser treinados periodicamente e visitantes devem receber instruções gerais. Cenários a contemplar:"));
                Lista(c, comp.CenariosEmergencia.Count > 0 ? comp.CenariosEmergencia : CenariosEmergenciaPgr.Padrao);
                Subtitulo(c, "Contatos de emergência");
                Tabela(c, new[] { ("Local / setor", 2.6f), ("Endereço", 3f), ("Telefone", 1.5f), ("Distância e tempo", 1.8f) },
                    comp.ContatosEmergencia.Select(x => new[]
                    {
                        x.Recomendado ? $"{x.Local} (recomendado)" : x.Local,
                        string.IsNullOrWhiteSpace(x.Endereco) ? "—" : x.Endereco!,
                        string.IsNullOrWhiteSpace(x.Telefone) ? "—" : x.Telefone!,
                        x.TempoMinutos is { } min ? $"{(x.DistanciaKm ?? 0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} km · {min} min" : "—",
                    }),
                    "Nenhum contato de emergência informado.");

                var comRota = comp.ContatosEmergencia.Where(x => !string.IsNullOrWhiteSpace(x.Rota)).ToList();
                if (comRota.Count > 0)
                {
                    var primeiraRota = true;
                    foreach (var x in comRota)
                    {
                        var abrir = primeiraRota; // o subtítulo vai junto da primeira rota, para não ficar sozinho no fim da página
                        primeiraRota = false;
                        c.Item().ShowEntire().Column(rc =>
                        {
                            if (abrir) Subtitulo(rc, "Como chegar a partir da obra");
                            rc.Item().PaddingTop(2).Text(x.Local).SemiBold();
                            var passos = x.Rota!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            for (var i = 0; i < passos.Length; i++)
                                rc.Item().PaddingLeft(6).Row(pr =>
                                {
                                    pr.ConstantItem(18).Text($"{i + 1}.").FontSize(8.5f).FontColor(Secundario);
                                    pr.RelativeItem().Text(passos[i]).FontSize(8.5f);
                                });
                        });
                    }
                    var calculadoEm = comp.RotasCalculadasEmUtc is { } quando ? $" em {HorarioBrasilia.De(quando):dd/MM/yyyy}" : string.Empty;
                    c.Item().PaddingTop(2).Text($"Rotas de carro calculadas com {comp.RotasFonte ?? "mapa online"}{calculadoEm}. Confirme o trajeto e os telefones no local antes de usar; trânsito e obras na via podem alterar o caminho.")
                        .FontSize(8).FontColor(Secundario).Italic();
                }

                // 11. Considerações finais
                // Data, aprovação e assinaturas ficam sempre juntos, na mesma página do título.
                TituloComPrimeiro(c, Secoes[10], b =>
                {
                    b.Item().Text($"{d.ObraNome}, {DataPorExtenso(HorarioBrasilia.De(d.AprovadoEmUtc ?? d.GeradoEmUtc))}.").FontColor(Secundario);
                    // Na minuta ainda não houve aprovação: afirmar o contrário seria falso.
                    if (!d.Minuta)
                        b.Item().Text(t =>
                        {
                            t.Span("Aprovado pelo Engenheiro de Segurança do Trabalho: ");
                            t.Span(d.AprovadoPorNome ?? "não informado").SemiBold();
                            if (d.AprovadoEmUtc is { } ap) t.Span($" em {HorarioBrasilia.De(ap):dd/MM/yyyy HH:mm}.");
                        });
                    if (d.Assinaturas is { Count: > 0 } assinaturas)
                    {
                        b.Item().PaddingTop(14).Row(r =>
                        {
                            for (var i = 0; i < assinaturas.Count; i++)
                            {
                                if (i > 0) r.ConstantItem(36);
                                AssinaturaPgr(r, assinaturas[i], d.Verificacao?.Codigo);
                            }
                        });
                        if (d.Verificacao is { } ver)
                            b.Item().PaddingTop(14).ShowEntire().Row(r =>
                            {
                                r.ConstantItem(70).Height(70).Image(ver.QrPng);
                                r.ConstantItem(12);
                                r.RelativeItem().Column(q =>
                                {
                                    q.Item().Text("Verificação de autenticidade").SemiBold().FontSize(9).FontColor(Marca);
                                    q.Item().Text("Aponte a câmera para o QR ou acesse o endereço abaixo e informe o código para conferir que este PGR é o documento assinado no sistema.").FontSize(8).FontColor(Secundario);
                                    q.Item().PaddingTop(3).Text(ver.Url).FontSize(8).FontColor(Texto);
                                    q.Item().Text($"Código: {ver.Codigo}").FontSize(8).SemiBold();
                                });
                            });
                        return;
                    }
                    b.Item().PaddingTop(34).Row(r =>
                    {
                        Assinatura(r, d.AprovadoPorNome ?? "Engenheiro de Segurança do Trabalho",
                            "Responsável técnico pela elaboração do PGR" + (string.IsNullOrWhiteSpace(comp.ResponsavelTecnicoRegistro) ? "" : $" · {comp.ResponsavelTecnicoRegistro}"));
                        r.ConstantItem(36);
                        Assinatura(r, string.IsNullOrWhiteSpace(comp.ResponsavelImplantacaoNome) ? " " : comp.ResponsavelImplantacaoNome,
                            "Responsável pela implantação do PGR" + (string.IsNullOrWhiteSpace(comp.ResponsavelImplantacaoCargo) ? "" : $" · {comp.ResponsavelImplantacaoCargo}"));
                    });
                });
            });
            Rodape(p, d);
        });
    }

    // ───────────────────────────── Peças reutilizáveis ─────────────────────────────

    private static void ConfigurarPagina(PageDescriptor p, MarcaDagua? marcaDagua)
    {
        p.Size(PageSizes.A4);
        p.Margin(2f, Unit.Centimetre);
        p.DefaultTextStyle(s => s.FontSize(9.5f).FontColor(Texto).LineHeight(1.35f));
        if (marcaDagua is not null)
            p.Background().AlignCenter().AlignMiddle().Width(marcaDagua.Largura).Image(marcaDagua.Png).FitWidth();
    }

    private static void Cabecalho(PageDescriptor p, DadosPgrPdf d) =>
        p.Header().PaddingBottom(14).Column(h =>
        {
            h.Item().Row(r =>
            {
                r.RelativeItem().Text("PGR · Programa de Gerenciamento de Riscos").FontSize(8).FontColor(Secundario);
                r.RelativeItem().AlignRight().Text(d.ObraNome).FontSize(8).FontColor(Secundario);
            });
            if (d.Minuta)
                h.Item().PaddingTop(3).Text("MINUTA PARA CONFERÊNCIA · NÃO ASSINADO · SEM VALOR DOCUMENTAL").FontSize(8).Bold().FontColor(Marca);
            h.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Linha);
        });

    private static void Rodape(PageDescriptor p, DadosPgrPdf d) =>
        p.Footer().Column(f =>
        {
            f.Item().LineHorizontal(0.5f).LineColor(Linha);
            f.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text(d.Verificacao is { } v ? $"AAHBRANT SST · código de verificação {v.Codigo}" : "AAHBRANT SST")
                    .FontSize(7.5f).FontColor(Secundario);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Secundario));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });

    private static void CabecalhoSecao(ColumnDescriptor col, Secao s)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(28).Text(s.Numero).FontSize(15).Bold().FontColor(Marca);
            r.RelativeItem().Text(s.Titulo).FontSize(15).Bold();
        });
        col.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor(Linha);
    }

    // Título de seção que começa por tabela ou campos (o conteúdo pode ser longo demais para um bloco único).
    private static void Titulo(ColumnDescriptor c, Secao s) =>
        c.Item().Section(s.Id).PaddingTop(16).Column(col => CabecalhoSecao(col, s));

    // Título e primeiro bloco de texto num bloco só: a quebra de página nunca deixa o título sozinho no fim da folha.
    private static void TituloComPrimeiro(ColumnDescriptor c, Secao s, Action<ColumnDescriptor> primeiro) =>
        c.Item().ShowEntire().Section(s.Id).PaddingTop(16).Column(col =>
        {
            col.Spacing(7);
            CabecalhoSecao(col, s);
            primeiro(col);
        });

    private static void Subtitulo(ColumnDescriptor c, string texto) =>
        c.Item().PaddingTop(4).Text(texto).FontSize(10).SemiBold().FontColor(Secundario);

    private static void Paragrafo(ColumnDescriptor c, string texto) =>
        c.Item().Text(texto).Justify();

    private static void Lista(ColumnDescriptor c, IEnumerable<string> itens)
    {
        foreach (var i in itens)
            c.Item().PaddingLeft(6).Row(r =>
            {
                r.ConstantItem(12).Text("•").FontColor(Marca);
                r.RelativeItem().Text(i);
            });
    }

    private static void Campo(TableDescriptor t, string rotulo, string? valor) =>
        t.Cell().PaddingBottom(7).Column(col =>
        {
            col.Item().Text(rotulo).FontSize(7.5f).FontColor(Secundario);
            if (string.IsNullOrWhiteSpace(valor)) col.Item().Text("não informado").Italic().FontColor(Secundario);
            else col.Item().Text(valor).SemiBold();
        });

    // Tabela minimalista: sem preenchimento no cabeçalho, só linhas finas.
    private static void Tabela(ColumnDescriptor c, (string Titulo, float Peso)[] colunas, IEnumerable<string[]> linhas, string vazio)
    {
        var lista = linhas.ToList();
        if (lista.Count == 0)
        {
            c.Item().Text(vazio).Italic().FontColor(Secundario);
            return;
        }
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { foreach (var col in colunas) cd.RelativeColumn(col.Peso); });
            t.Header(h =>
            {
                foreach (var col in colunas)
                    h.Cell().BorderBottom(1).BorderColor(Texto).PaddingVertical(4).PaddingRight(6)
                        .Text(col.Titulo.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Secundario);
            });
            foreach (var linha in lista)
                foreach (var valor in linha)
                    t.Cell().ShowEntire().BorderBottom(0.5f).BorderColor(Linha).PaddingVertical(5).PaddingRight(6).Text(valor).FontSize(9);
        });
    }

    // Uma assinatura do PGR: carimbo em cima da linha quando assinou no sistema, espaço em branco quando é à mão ou quando ainda
    // não assinou (minuta). O carimbo descreve só o que de fato ocorreu: assinatura por sessão autenticada, sem citar certificado.
    private static void AssinaturaPgr(RowDescriptor r, AssinaturaPgrPdf a, string? codigo) =>
        r.RelativeItem().Column(col =>
        {
            if (a.AssinadoEmUtc is { } quando)
            {
                col.Item().Border(0.8f).BorderColor(Marca).Padding(6).Column(s =>
                {
                    s.Item().Text("ASSINADO ELETRONICAMENTE").Bold().FontSize(7.5f).FontColor(Marca);
                    s.Item().PaddingTop(2).Text(a.Nome ?? " ").SemiBold().FontSize(9.5f);
                    if (!string.IsNullOrWhiteSpace(a.Registro))
                        s.Item().Text(a.Registro).FontSize(8);
                    s.Item().PaddingTop(2).Text($"Em {HorarioBrasilia.De(quando):dd/MM/yyyy 'às' HH:mm} (Brasília)").FontSize(8);
                    s.Item().Text("Autenticação por sessão no sistema AAHBRANT SST").FontSize(7.5f).FontColor(Secundario);
                    if (!string.IsNullOrWhiteSpace(codigo))
                        s.Item().Text($"Código {codigo}").FontSize(7.5f).FontColor(Secundario);
                });
                col.Item().Height(6);
            }
            else
            {
                col.Item().Height(a.AMao ? 40 : 30).AlignBottom().Text(a.AMao ? string.Empty : "Aguardando assinatura").FontSize(8).Italic().FontColor(Secundario);
            }
            col.Item().BorderTop(0.6f).BorderColor(Texto).PaddingTop(4).Text(string.IsNullOrWhiteSpace(a.Nome) ? " " : a.Nome).SemiBold().FontSize(9);
            col.Item().Text(a.Descricao + (a.AssinadoEmUtc is null && !string.IsNullOrWhiteSpace(a.Registro) ? $" · {a.Registro}" : string.Empty))
                .FontSize(8).FontColor(Secundario);
        });

    private static void Assinatura(RowDescriptor r, string nome, string papel) =>
        r.RelativeItem().Column(col =>
        {
            col.Item().Height(26);
            col.Item().BorderTop(0.6f).BorderColor(Texto).PaddingTop(4).Text(nome).SemiBold().FontSize(9);
            col.Item().Text(papel).FontSize(8).FontColor(Secundario);
        });

    // ───────────────────────────── Matriz e fichas de risco ─────────────────────────────

    private static void MatrizRisco(ColumnDescriptor c, MatrizRiscoPdf? m)
    {
        if (m is null || m.Celulas.Count == 0)
        {
            c.Item().Text("Matriz de risco não cadastrada.").Italic().FontColor(Secundario);
            return;
        }
        c.Item().Text("Probabilidade (linhas) × severidade (colunas). O nível de cada risco vem do cruzamento das duas.")
            .FontSize(8.5f).FontColor(Secundario);
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(54);
                for (var s = 1; s <= m.MaxSeveridade; s++) cd.RelativeColumn();
            });
            for (var p = m.MaxProbabilidade; p >= 1; p--)
            {
                t.Cell().PaddingVertical(5).Text($"P {p}").FontSize(8).SemiBold().FontColor(Secundario);
                for (var s = 1; s <= m.MaxSeveridade; s++)
                {
                    var nivel = m.Celulas.FirstOrDefault(x => x.Probabilidade == p && x.Severidade == s)?.Nivel;
                    t.Cell().Background(nivel is null ? "#ffffff" : CorNivel(nivel.Value)).Border(0.5f).BorderColor("#ffffff")
                        .PaddingVertical(6).AlignCenter().Text(nivel is null ? "—" : NomeNivel(nivel.Value)).FontSize(7.5f);
                }
            }
            t.Cell().Text("");
            for (var s = 1; s <= m.MaxSeveridade; s++)
                t.Cell().PaddingTop(3).AlignCenter().Text($"S {s}").FontSize(8).SemiBold().FontColor(Secundario);
        });
    }

    // Ficha de risco, no desenho do modelo: identificação, prevenção e controle, exposição e avaliação.
    // Cronograma mensal do modelo da obra: uma grade de janeiro a dezembro por ano, com os meses de cada ação marcados
    // de Início a Prazo (sem início, só o mês do prazo). Ação sem prazo fica só na tabela acima.
    private static void Cronograma(ColumnDescriptor c, List<AcaoPlanoGerada> acoes)
    {
        var numeradas = acoes.Select((a, i) => (Numero: i + 1, Acao: a)).Where(x => x.Acao.Prazo is not null).ToList();
        if (numeradas.Count == 0) return;
        var primeiroAno = numeradas.Min(x => (x.Acao.Inicio is { } ini && ini <= x.Acao.Prazo ? ini : x.Acao.Prazo!.Value).Year);
        var ultimoAno = numeradas.Max(x => x.Acao.Prazo!.Value.Year);
        string[] meses = { "JAN", "FEV", "MAR", "ABR", "MAI", "JUN", "JUL", "AGO", "SET", "OUT", "NOV", "DEZ" };

        for (var ano = primeiroAno; ano <= Math.Min(ultimoAno, primeiroAno + 2); ano++)
        {
            var anoAtual = ano;
            var abrir = ano == primeiroAno; // o subtítulo vai junto da primeira grade, para não ficar sozinho no fim da página
            c.Item().ShowEntire().PaddingTop(2).Column(bloco =>
            {
                if (abrir) Subtitulo(bloco, "Cronograma");
                bloco.Item().Table(t =>
                {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3f);
                    for (var m = 0; m < 12; m++) cd.RelativeColumn(0.5f);
                });
                t.Header(h =>
                {
                    h.Cell().BorderBottom(1).BorderColor(Texto).PaddingVertical(4).Text($"AÇÃO · {anoAtual}").FontSize(7.5f).SemiBold().FontColor(Secundario);
                    foreach (var mes in meses)
                        h.Cell().BorderBottom(1).BorderColor(Texto).PaddingVertical(4).AlignCenter().Text(mes).FontSize(6.5f).SemiBold().FontColor(Secundario);
                });
                foreach (var (numero, acao) in numeradas)
                {
                    var ativos = CronogramaPlano.MesesAtivos(acao.Inicio, acao.Prazo, anoAtual);
                    var resumo = acao.Descricao.Trim();
                    if (resumo.Length > 44) resumo = resumo[..43] + "…";
                    t.Cell().BorderBottom(0.5f).BorderColor(Linha).PaddingVertical(4).PaddingRight(4).Text($"{numero:00} · {resumo}").FontSize(8);
                    foreach (var ativo in ativos)
                        t.Cell().BorderBottom(0.5f).BorderColor(Linha).Background(ativo ? "#e2bcbc" : "#ffffff").Height(16).Text("");
                }
                });
            });
        }
    }

    private static void FichaRisco(ColumnDescriptor c, MatrizRiscoPdf? m, AtividadeGerada atividade, RiscoGerado r,
        IReadOnlyDictionary<Guid, CatalogoEpiInfo>? epis)
    {
        var nivel = NivelDe(m, r);
        var medidas = r.Medidas
            .Where(x => x.Decisao == "Aceita" && !string.IsNullOrWhiteSpace(x.Texto))
            .OrderBy(x => x.Fase)
            .ToList();

        c.Item().ShowEntire().PaddingTop(8).Border(0.5f).BorderColor(Linha).Column(f =>
        {
            f.Item().Background(Fundo).Padding(7).Row(h =>
            {
                h.RelativeItem().Text(r.Perigo).SemiBold();
                h.ConstantItem(110).AlignRight().Text(r.Tipo).FontSize(8.5f).FontColor(Secundario);
            });
            f.Item().Padding(7).Column(b =>
            {
                b.Spacing(4);
                Grupo(b, "Identificação");
                Linha2(b, "Possíveis lesões ou agravos à saúde", r.Danos);
                Linha2(b, "Fontes ou circunstâncias", $"EAP {atividade.CodigoEap} — {atividade.Nome}");
                if (r.Base.Count > 0) Linha2(b, "Requisitos legais", string.Join(", ", r.Base));
                Grupo(b, "Prevenção e controle");
                if (medidas.Count == 0) b.Item().Text("Nenhuma medida aceita.").FontSize(8.5f).FontColor(Secundario).Italic();
                else
                {
                    // Na ordem de prioridade da NR-01; medida sem classificação (rascunho antigo) vai por último.
                    var grupos = CategoriasMedida.Todas.Select(cat => (Rotulo: RotuloCategoria(cat), Itens: medidas.Where(x => x.Categoria == cat).ToList()))
                        .Append((Rotulo: "Outras medidas", Itens: medidas.Where(x => !CategoriasMedida.Valida(x.Categoria)).ToList()))
                        .Where(x => x.Itens.Count > 0);
                    foreach (var (rotulo, itens) in grupos)
                        b.Item().Row(lin =>
                        {
                            lin.ConstantItem(150).Text(rotulo).FontSize(8.5f).FontColor(Secundario);
                            lin.RelativeItem().Column(col =>
                            {
                                foreach (var x in itens)
                                    col.Item().Text(t =>
                                    {
                                        t.Span(x.Texto.Trim()).FontSize(8.5f);
                                        // EPI ligado ao catálogo: nome, CA e validade (NR-06) logo depois do texto da medida.
                                        if (x.CatalogoEpiId is { } epiId && epis is not null && epis.TryGetValue(epiId, out var epi))
                                            t.Span($" — {epi.Nome}, {(string.IsNullOrWhiteSpace(epi.Ca) ? "sem CA informado" : $"CA {epi.Ca}")}"
                                                   + (epi.CaValidade is { } val ? $" (validade {val:dd/MM/yyyy})" : string.Empty)).FontSize(8).SemiBold();
                                        t.Span($"  ({NomeFase(x.Fase)})").FontSize(7.5f).FontColor(Secundario);
                                    });
                            });
                        });
                }
                Grupo(b, "Exposição");
                var exposicao = r.Exposicao ?? new ExposicaoRisco();
                Linha2(b, "Critério", exposicao.Criterio == CriteriosExposicao.Quantitativo ? "Quantitativo, com medição" : "Qualitativo, pela matriz de risco");
                if (!string.IsNullOrWhiteSpace(exposicao.Perfil)) Linha2(b, "Perfil de exposição", exposicao.Perfil);
                if (exposicao.Medicoes.Count > 0)
                    Tabela(b, new[] { ("Data", 1.1f), ("Medição", 1.2f), ("Limite", 1.1f), ("Nível de ação", 1.1f), ("Empresa", 1.4f), ("Técnica", 1.3f), ("Equipamento", 1.4f) },
                        exposicao.Medicoes.Select(med => new[]
                        {
                            med.Data?.ToString("dd/MM/yyyy") ?? "—",
                            med.Valor is { } v ? $"{v.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} {med.Unidade}".Trim() : "—",
                            med.LimiteTolerancia ?? "—", med.NivelAcao ?? "—", med.Empresa ?? "—", med.Tecnica ?? "—", med.Equipamento ?? "—",
                        }),
                        "");
                Grupo(b, "Avaliação de risco");
                b.Item().Row(a =>
                {
                    Metrica(a, "Severidade", $"S {r.Severidade}");
                    Metrica(a, "Probabilidade", $"P {r.Probabilidade}");
                    Metrica(a, "Classificação", Classificacao(r, nivel), nivel is NivelRisco.Alto or NivelRisco.Critico);
                    Metrica(a, "Conduta", nivel is null ? "—" : Conduta(nivel.Value));
                });
            });
        });
    }

    private static void Grupo(ColumnDescriptor b, string titulo) =>
        b.Item().PaddingTop(2).Text(titulo.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Marca);

    private static void Linha2(ColumnDescriptor b, string rotulo, string? valor) =>
        b.Item().Row(r =>
        {
            r.ConstantItem(150).Text(rotulo).FontSize(8.5f).FontColor(Secundario);
            r.RelativeItem().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor).FontSize(8.5f);
        });

    private static void Metrica(RowDescriptor r, string rotulo, string valor, bool destaque = false) =>
        r.RelativeItem().Column(col =>
        {
            col.Item().Text(rotulo).FontSize(7.5f).FontColor(Secundario);
            var texto = col.Item().Text(valor).FontSize(9.5f).SemiBold();
            if (destaque) texto.FontColor(Marca);
        });

    // ───────────────────────────── Nomes e cores ─────────────────────────────

    private static NivelRisco? NivelDe(MatrizRiscoPdf? m, RiscoGerado r) =>
        m?.Celulas.FirstOrDefault(x => x.Probabilidade == r.Probabilidade && x.Severidade == r.Severidade)?.Nivel;

    // Formato do modelo da obra: produto P×S e nível, como "15 – Alto".
    private static string Classificacao(RiscoGerado r, NivelRisco? nivel) =>
        $"{r.Probabilidade * r.Severidade} – {(nivel is null ? "sem nível" : NomeNivel(nivel.Value))}";

    private static string RotuloCategoria(string categoria) => categoria switch
    {
        CategoriasMedida.Eliminacao => "Eliminação",
        CategoriasMedida.Epc => "EPC (proteção coletiva)",
        CategoriasMedida.Administrativa => "Administrativas e procedimentos",
        CategoriasMedida.Epi => "EPI (proteção individual)",
        _ => categoria,
    };

    // Sem depender da cultura do servidor (container pode rodar em modo invariante).
    private static string DataPorExtenso(DateTime data)
    {
        string[] meses = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
        return $"{data.Day} de {meses[data.Month - 1]} de {data.Year}";
    }

    private static string NomeFase(int fase) => fase switch
    {
        1 => "Antes",
        2 => "Durante",
        3 => "Após",
        _ => "Medida",
    };

    private static string NomeNivel(NivelRisco n) => n switch
    {
        NivelRisco.Trivial => "Trivial",
        NivelRisco.Baixo => "Baixo",
        NivelRisco.Moderado => "Moderado",
        NivelRisco.Alto => "Alto",
        NivelRisco.Critico => "Crítico",
        _ => n.ToString(),
    };

    private static string Conduta(NivelRisco n) => n switch
    {
        NivelRisco.Trivial or NivelRisco.Baixo => "Manter controles",
        NivelRisco.Moderado => "Avaliar novos controles",
        NivelRisco.Alto => "Implementar novos controles",
        NivelRisco.Critico => "Paralisar a atividade",
        _ => "—",
    };

    private static string CorNivel(NivelRisco n) => n switch
    {
        NivelRisco.Trivial => "#fafafa",
        NivelRisco.Baixo => "#f0f0f0",
        NivelRisco.Moderado => "#e2e2e2",
        NivelRisco.Alto => "#cfcfcf",
        NivelRisco.Critico => "#e8c9c9",
        _ => "#ffffff",
    };
}
