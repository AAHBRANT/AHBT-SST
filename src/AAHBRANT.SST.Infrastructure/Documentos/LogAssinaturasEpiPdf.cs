using System.Globalization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Páginas finais da Ficha de EPI com o log de assinaturas do funcionário (pedido do usuário, 06/10).
// Página A: certificado de conclusão (resumo, rastreamento, uma linha por assinatura, eventos do
// documento), no estilo do certificado do DocuSign. Páginas B: uma por assinatura, com o cupom da
// entrega, as fotos/digitais de cadastro e da assinatura lado a lado (marca d'água CONFIDENCIAL), a
// validação (Azure AI Face ou agente local) e o rastro técnico. As páginas da ficha em si não mudam.
internal static class LogAssinaturasEpiPdf
{
    private const string CorMarca = "#670000";
    private const string CorAzul = "#0f5ba8";
    private const string CorAzulFundo = "#eef4fb";
    private const string CorBarra = "#e8e5de";
    private const string CorLinha = "#d9d3c9";
    private const string CorSuave = "#6f6666";
    private const string CorFundoSuave = "#f5f3ee";

    public static void Adicionar(IDocumentContainer documento, FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log)
    {
        documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, modelo, "Ficha de EPI — Log de Assinaturas");
            pagina.Content().PaddingVertical(8).Column(coluna => PaginaCertificado(coluna, modelo, log));
        });

        for (var i = 0; i < log.Itens.Count; i++)
        {
            var item = log.Itens[i];
            var numero = i + 1;
            documento.Page(pagina =>
            {
                ConfigurarPagina(pagina, modelo, "Ficha de EPI — Log de Assinaturas");
                pagina.Content().PaddingVertical(8).Column(coluna => PaginaEvidencias(coluna, modelo, log, item, numero));
            });
        }
    }

    private static void ConfigurarPagina(PageDescriptor pagina, FichaEpiPdfModelo modelo, string titulo)
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(2, Unit.Centimetre);
        pagina.DefaultTextStyle(estilo => estilo.FontSize(8.5f));
        pagina.Header().Column(coluna => CabecalhoDocumentoPadrao.Desenhar(coluna, titulo, modelo.ObraNome, modelo.ObraLogoConteudo));
        pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
            coluna, "Ficha de EPI", protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
    }

    // ───────────────────────────── Página A: certificado de conclusão ─────────────────────────────

    private static void PaginaCertificado(ColumnDescriptor coluna, FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log)
    {
        coluna.Spacing(6);
        coluna.Item().Text("Certificado de Conclusão — Assinaturas de EPI").FontSize(13).Bold().FontColor(CorMarca);

        coluna.Item().Element(c => Barra(c, "Resumo"));
        coluna.Item().Row(l =>
        {
            l.Spacing(10);
            l.RelativeItem(3).Column(c =>
            {
                c.Item().Element(x => Campo(x, "ID do registro", log.IdRegistro));
                c.Item().Element(x => Campo(x, "Funcionário", $"{modelo.TrabalhadorNome} · Mat. {modelo.TrabalhadorMatricula}"));
            });
            l.RelativeItem(2).Column(c =>
            {
                c.Item().Element(x => Campo(x, "Situação", "Concluído"));
                c.Item().Element(x => Campo(x, "Assinaturas de EPI", log.TotalAssinaturas.ToString(CultureInfo.InvariantCulture)));
            });
            l.RelativeItem(3).Column(c =>
            {
                c.Item().Element(x => Campo(x, "Emitido em", Data(log.EmitidoEmUtc) + " (Brasília, UTC-3)"));
                c.Item().Element(x => Campo(x, "Origem", $"Sistema SST · {modelo.ObraNome}"));
            });
        });
        coluna.Item().Element(x => Campo(x, "Hash SHA-256 da ficha", modelo.ConteudoHash));

        coluna.Item().PaddingTop(2).Element(c => Barra(c, "Eventos de assinatura"));
        foreach (var item in log.Itens)
            coluna.Item().ShowEntire().Element(c => LinhaEvento(c, modelo, log, item));

        coluna.Item().PaddingTop(2).Element(c => Barra(c, "Eventos do documento"));
        coluna.Item().Element(c => LinhaDocumento(c, "Log gerado a partir dos registros do sistema", "Registrado", Data(log.EmitidoEmUtc)));
        coluna.Item().Element(c => LinhaDocumento(c, "Assinaturas conferidas", "Integridade verificada (SHA-256 da ficha acima)", Data(log.EmitidoEmUtc)));
    }

    private static void LinhaEvento(IContainer container, FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log, LogAssinaturaEpiItem item)
    {
        container.BorderBottom(0.5f).BorderColor(CorLinha).PaddingVertical(5).Row(linha =>
        {
            linha.Spacing(8);

            linha.RelativeItem(4).Column(c =>
            {
                c.Spacing(1.5f);
                c.Item().Text(modelo.TrabalhadorNome).Bold().FontSize(9);
                c.Item().Text($"{modelo.TrabalhadorFuncaoNome} · Mat. {modelo.TrabalhadorMatricula}").FontSize(7.5f);
                c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Documento: ").SemiBold(); t.Span(item.Descricao); });
                c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Nível de segurança: ").SemiBold(); t.Span(DescreverSeguranca(item.Metodo)); });
                if (item.Metodo is MetodoAutenticacaoAssinatura.Biometria or MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
                {
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(7)); t.Span("Termo de aceite aceito: ").SemiBold(); t.Span(log.TermoAceiteEm is { } a ? Data(a) : "não informado"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(7)); t.Span("Consentimento LGPD: ").SemiBold(); t.Span(log.ConsentimentoLgpdEm is { } b ? Data(b) : "não informado"); });
                }
            });

            linha.RelativeItem(4).Column(c =>
            {
                c.Spacing(1.5f);
                c.Item().Row(r =>
                {
                    r.Spacing(5);
                    if (item.EvidenciaAssinatura is { Length: > 0 } ev && EhImagem(ev))
                        r.ConstantItem(26).Height(34).Element(x => ImagemComMarca(x, ev, Data(item.AssinadoEmUtc), pequena: true));
                    r.RelativeItem().Column(s =>
                    {
                        s.Item().Text("Assinado por:").FontSize(6.5f).FontColor(CorSuave);
                        s.Item().Text(HashCurto(item.EvidenciaAssinaturaHash) ?? "—").FontSize(7).SemiBold();
                    });
                });
                c.Item().Text($"IP: {item.Ip ?? "não registrado"}").FontSize(7.5f);
                c.Item().Text($"Equipamento: {Equipamento(item.UserAgent)}").FontSize(7.5f);
                c.Item().Text($"Local: {Localizacao(item)}").FontSize(7.5f);
                c.Item().Element(x => CaixaValidacao(x, item, compacta: true));
            });

            linha.RelativeItem(2.4f).Column(c =>
            {
                c.Spacing(1.5f);
                if (item.CriadoEmUtc is { } criado) c.Item().Text($"Criada: {Data(criado)}").FontSize(7.5f);
                c.Item().Text($"Assinada: {Data(item.AssinadoEmUtc)}").FontSize(7.5f).Bold();
            });
        });
    }

    private static void LinhaDocumento(IContainer container, string evento, string estado, string data)
    {
        container.BorderBottom(0.5f).BorderColor(CorLinha).PaddingVertical(3).Row(r =>
        {
            r.RelativeItem(4).Text(evento).SemiBold().FontSize(8);
            r.RelativeItem(4).Text(estado).FontSize(8);
            r.RelativeItem(2.4f).Text(data).FontSize(8);
        });
    }

    // ───────────────────────────── Página B: evidências de uma assinatura ─────────────────────────────

    private static void PaginaEvidencias(ColumnDescriptor coluna, FichaEpiPdfModelo modelo, LogAssinaturasFichaEpi log, LogAssinaturaEpiItem item, int numero)
    {
        coluna.Spacing(7);
        coluna.Item().Row(r =>
        {
            r.RelativeItem().Text($"Evidências da assinatura {numero} de {log.Itens.Count}").FontSize(12).Bold().FontColor(CorMarca);
            r.AutoItem().Text(Data(item.AssinadoEmUtc)).FontSize(8).FontColor(CorSuave);
        });
        coluna.Item().Text(item.Descricao).FontSize(9).SemiBold();
        coluna.Item().Text($"{modelo.TrabalhadorNome} · {modelo.TrabalhadorFuncaoNome} · Mat. {modelo.TrabalhadorMatricula}").FontSize(8).FontColor(CorSuave);

        coluna.Item().Element(c => CaixaValidacao(c, item, compacta: false));

        coluna.Item().Text($"Data e hora da assinatura: {Data(item.AssinadoEmUtc)} (Brasília)").FontSize(8.5f).AlignCenter();

        coluna.Item().Row(linha =>
        {
            linha.Spacing(10);
            if (item.Cupom is { } cupom)
                linha.ConstantItem(185).Element(c => Cupom(c, modelo, cupom, item));
            linha.RelativeItem().Element(c => GradeEvidencias(c, item));
        });

        coluna.Item().Element(c => DadosTecnicos(c, item));
    }

    private static void GradeEvidencias(IContainer container, LogAssinaturaEpiItem item)
    {
        var dataHora = Data(item.AssinadoEmUtc);
        container.Border(0.5f).BorderColor(CorLinha).Column(coluna =>
        {
            if (item.Metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
            {
                coluna.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => CelulaImagem(c, item.FotoCadastro, "Foto de cadastro (referência)",
                        item.FotoCadastroEmUtc is { } d ? $"Capturada em {Data(d)}\nSHA-256 {HashCurto(item.FotoCadastroHash) ?? "—"}" : null,
                        "Sem foto de cadastro vigente na data", dataHora));
                    r.RelativeItem().Element(c => CelulaImagem(c, item.EvidenciaAssinatura, "Foto da assinatura",
                        $"Capturada em {dataHora}\nSHA-256 {HashCurto(item.EvidenciaAssinaturaHash) ?? "—"}",
                        "Foto não registrada (anterior à implantação)", dataHora));
                });
            }
            else if (item.Metodo == MetodoAutenticacaoAssinatura.Biometria)
            {
                coluna.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => CelulaImagem(c, item.DigitalCadastroImagem, "Digital cadastrada (referência)",
                        item.DigitalCadastroEmUtc is { } d ? $"Cadastro de {Data(d)}" : null,
                        item.DigitalCadastroEmUtc is { } d2
                            ? $"Cadastro de {Data(d2)} sem imagem (anterior à implantação)"
                            : "Cadastro não localizado", dataHora));
                    r.RelativeItem().Element(c => CelulaImagem(c, item.EvidenciaAssinatura, "Digital da assinatura",
                        $"Leitura em {dataHora}\nSHA-256 {HashCurto(item.EvidenciaAssinaturaHash) ?? "—"}",
                        "Imagem não registrada (anterior à implantação)", dataHora));
                });
            }
            else
            {
                coluna.Item().Padding(10).Text("Assinatura por sessão autenticada do usuário. Não há imagem biométrica associada.")
                    .FontSize(8).Italic().FontColor(CorSuave);
            }
        });
    }

    private static void CelulaImagem(IContainer container, byte[]? imagem, string titulo, string? legenda, string textoAusente, string dataHora)
    {
        container.Padding(6).Column(c =>
        {
            c.Spacing(3);
            if (imagem is { Length: > 0 } && EhImagem(imagem))
                c.Item().AlignCenter().Width(104).Height(132).Element(x => ImagemComMarca(x, imagem, dataHora, pequena: false));
            else
                c.Item().AlignCenter().Width(104).Height(132).Border(0.6f).BorderColor(CorLinha).Background(CorFundoSuave)
                    .AlignCenter().AlignMiddle().Padding(6).Text(textoAusente).FontSize(7).FontColor(CorSuave).AlignCenter();
            c.Item().AlignCenter().Text(titulo).FontSize(7.5f).Bold();
            if (legenda is not null) c.Item().AlignCenter().Text(legenda).FontSize(6.5f).FontColor(CorSuave).AlignCenter();
        });
    }

    // Marca d'água "CONFIDENCIAL" + data e hora por cima da imagem biométrica (dado sensível — LGPD).
    // Texto branco com sombra escura por baixo, para continuar legível sobre fundo claro ou escuro.
    private static void ImagemComMarca(IContainer container, byte[] imagem, string dataHora, bool pequena)
    {
        var branco = Color.FromARGB(215, 255, 255, 255);
        var sombra = Color.FromARGB(190, 0, 0, 0);
        var tamanho = pequena ? 4.5f : 12f;

        void Texto(IContainer c, Color cor)
        {
            c.AlignCenter().AlignMiddle().Rotate(-28).Column(col =>
            {
                col.Item().AlignCenter().Text("CONFIDENCIAL").FontSize(tamanho).Bold().FontColor(cor);
                if (!pequena) col.Item().AlignCenter().Text(dataHora).FontSize(6.5f).FontColor(cor);
            });
        }

        container.Layers(camadas =>
        {
            camadas.Layer().Image(imagem).FitArea();
            camadas.Layer().TranslateX(0.6f).TranslateY(0.6f).Element(c => Texto(c, sombra));
            camadas.PrimaryLayer().Element(c => Texto(c, branco));
        });
    }

    private static void Cupom(IContainer container, FichaEpiPdfModelo modelo, CupomEntregaEpi cupom, LogAssinaturaEpiItem item)
    {
        container.Border(0.6f).BorderColor(CorLinha).Background("#fffefb").Padding(8).Column(c =>
        {
            c.Spacing(3);
            c.Item().AlignCenter().Text("EPIs recebidos").FontSize(11).Bold().FontColor(CorMarca);
            c.Item().AlignCenter().Text($"Cupom da entrega assinada em {Data(item.AssinadoEmUtc)}").FontSize(6.5f).FontColor(CorSuave);

            c.Item().Element(x => TituloCupom(x, "Identificação"));
            c.Item().Element(x => LinhaCupom(x, "Funcionário", modelo.TrabalhadorNome));
            c.Item().Element(x => LinhaCupom(x, "Função", modelo.TrabalhadorFuncaoNome));
            c.Item().Element(x => LinhaCupom(x, "Obra", modelo.ObraNome));
            c.Item().Element(x => LinhaCupom(x, "Data", cupom.DataEntrega.ToString("dd/MM/yyyy")));

            c.Item().Element(x => TituloCupom(x, "Itens do carrinho"));
            foreach (var epi in cupom.Itens)
            {
                c.Item().BorderTop(0.4f).BorderColor(CorLinha).PaddingVertical(3).Row(r =>
                {
                    r.Spacing(4);
                    if (epi.Foto is { Length: > 0 } foto && EhImagem(foto))
                        r.ConstantItem(30).Height(30).Border(0.4f).BorderColor(CorLinha).Image(foto).FitArea();
                    r.RelativeItem().Column(i =>
                    {
                        i.Item().Text(epi.Nome).Bold().FontSize(8);
                        i.Item().Text($"Fabricante: {epi.Fabricante ?? "—"}  ·  Qtd: {epi.Quantidade}").FontSize(6.5f);
                        i.Item().Text($"CA: {epi.CertificadoAprovacao ?? "—"}  ·  Val. CA: {Dia(epi.ValidadeCertificado)}").FontSize(6.5f);
                        i.Item().Text($"Val. entrega: {Dia(epi.ValidadeEntrega)}").FontSize(6.5f);
                    });
                });
            }

            c.Item().Element(x => TituloCupom(x, "Documentação e motivo"));
            c.Item().Element(x => LinhaCupom(x, "Motivo", cupom.Motivo ?? "—"));
            if (!string.IsNullOrWhiteSpace(cupom.ObservacaoMotivo)) c.Item().Element(x => LinhaCupom(x, "Obs. motivo", cupom.ObservacaoMotivo!));
            c.Item().Element(x => LinhaCupom(x, "Lista NR-6", cupom.ListaNr6 ?? "—"));
            c.Item().Element(x => LinhaCupom(x, "Trein. NR-6", Dia(cupom.TreinamentoNr6)));
            c.Item().Element(x => LinhaCupom(x, "Responsável", cupom.Responsavel ?? "—"));
        });
    }

    private static void TituloCupom(IContainer c, string texto) =>
        c.PaddingTop(3).BorderTop(0.4f).BorderColor(CorLinha).Text(texto.ToUpperInvariant()).FontSize(6.5f).Bold().FontColor(CorMarca);

    private static void LinhaCupom(IContainer c, string rotulo, string valor) =>
        c.Row(r =>
        {
            r.AutoItem().Text(rotulo).FontSize(6.5f).SemiBold().FontColor(CorSuave);
            r.ConstantItem(4);
            r.RelativeItem().AlignRight().Text(valor).FontSize(6.5f);
        });

    private static void DadosTecnicos(IContainer container, LogAssinaturaEpiItem item)
    {
        container.Border(0.5f).BorderColor(CorLinha).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Element(x => CelulaDado(x, "IP", item.Ip ?? "não registrado"));
                r.RelativeItem(1.4f).Element(x => CelulaDado(x, "Geolocalização (informada pelo aparelho)", Localizacao(item)));
                r.RelativeItem(1.2f).Element(x => CelulaDado(x, "Equipamento", Equipamento(item.UserAgent)));
            });
            c.Item().Row(r =>
            {
                r.RelativeItem().Element(x => CelulaDado(x, "Método", DescreverMetodo(item.Metodo)));
                r.RelativeItem(1.4f).Element(x => CelulaDado(x, "Leitor", item.LeitorId is { } id ? id.ToString("N")[..8] + "…" : "não se aplica"));
                r.RelativeItem(1.2f).Element(x => CelulaDado(x, "Assinada em", Data(item.AssinadoEmUtc)));
            });
        });
    }

    private static void CelulaDado(IContainer c, string rotulo, string valor) =>
        c.Border(0.3f).BorderColor(CorLinha).Padding(4).Column(x =>
        {
            x.Item().Text(rotulo).FontSize(6.5f).SemiBold().FontColor(CorMarca);
            x.Item().Text(valor).FontSize(8);
        });

    // ───────────────────────────── Validação (Azure / agente local / sessão) ─────────────────────────────

    private static void CaixaValidacao(IContainer container, LogAssinaturaEpiItem item, bool compacta)
    {
        var tamanho = compacta ? 6.5f : 8f;
        switch (item.Metodo)
        {
            case MetodoAutenticacaoAssinatura.ReconhecimentoFacial:
                container.Background(CorAzulFundo).BorderLeft(3).BorderColor(CorAzul).Padding(compacta ? 3 : 6).Column(c =>
                {
                    c.Item().Text("VALIDAÇÃO MICROSOFT AZURE AI FACE").Bold().FontSize(tamanho + 0.5f).FontColor(CorAzul);
                    if (compacta)
                    {
                        c.Item().Text("Identificação 1:N no grupo de pessoas da obra · reconhecido e aprovado").FontSize(tamanho);
                        return;
                    }
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Operação: ").SemiBold(); t.Span("identificação 1:N no grupo de pessoas da obra"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Modelo de reconhecimento: ").SemiBold(); t.Span(item.ValidacaoModelo ?? "não informado"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Grupo da obra: ").SemiBold(); t.Span(item.ValidacaoGrupoId ?? "não informado"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Resultado: ").SemiBold(); t.Span("reconhecido e aprovado (acima do limiar exigido)"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("ID da requisição Azure: ").SemiBold(); t.Span(item.ValidacaoRequisicaoId ?? "não informado"); });
                });
                break;

            case MetodoAutenticacaoAssinatura.Biometria:
                container.Background(CorFundoSuave).BorderLeft(3).BorderColor(CorMarca).Padding(compacta ? 3 : 6).Column(c =>
                {
                    c.Item().Text("VALIDAÇÃO DA DIGITAL").Bold().FontSize(tamanho + 0.5f).FontColor(CorMarca);
                    c.Item().Text("Comparação 1:N no agente do leitor Futronic FS80H (SourceAFIS)").FontSize(tamanho);
                    if (!compacta)
                        c.Item().Text("Conferida pelo servidor: leitor registrado, funcionário da obra, termo de aceite e consentimento LGPD válidos, resultado acima do limiar exigido.")
                            .FontSize(tamanho);
                });
                break;

            default:
                container.Background(CorFundoSuave).BorderLeft(3).BorderColor(CorMarca).Padding(compacta ? 3 : 6).Column(c =>
                {
                    c.Item().Text("SESSÃO AUTENTICADA").Bold().FontSize(tamanho + 0.5f).FontColor(CorMarca);
                    c.Item().Text("Assinatura com o login do próprio usuário no sistema").FontSize(tamanho);
                });
                break;
        }
    }

    // ───────────────────────────── Auxiliares ─────────────────────────────

    private static void Barra(IContainer c, string texto) =>
        c.Background(CorBarra).PaddingVertical(2).PaddingHorizontal(6).Text(texto).Bold().FontSize(9);

    private static void Campo(IContainer c, string rotulo, string valor) =>
        c.PaddingVertical(1).Text(t => { t.Span(rotulo + ": ").SemiBold().FontSize(8); t.Span(valor).FontSize(8); });

    private static string Data(DateTime utc) => HorarioBrasilia.De(utc).ToString("dd/MM/yyyy HH:mm:ss");

    private static string Dia(DateTime? data) => data is { } d ? d.ToString("dd/MM/yyyy") : "—";

    private static string? HashCurto(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? null : (hash.Length <= 12 ? hash : hash[..12] + "…");

    private static string DescreverMetodo(MetodoAutenticacaoAssinatura metodo) => metodo switch
    {
        MetodoAutenticacaoAssinatura.Biometria => "Digital (Futronic FS80H)",
        MetodoAutenticacaoAssinatura.ReconhecimentoFacial => "Reconhecimento facial",
        MetodoAutenticacaoAssinatura.SessaoLogada => "Sessão logada",
        _ => metodo.ToString(),
    };

    private static string DescreverSeguranca(MetodoAutenticacaoAssinatura metodo) => metodo switch
    {
        MetodoAutenticacaoAssinatura.Biometria => "biometria digital com cadastro prévio",
        MetodoAutenticacaoAssinatura.ReconhecimentoFacial => "reconhecimento facial com cadastro prévio",
        MetodoAutenticacaoAssinatura.SessaoLogada => "sessão autenticada do usuário",
        _ => metodo.ToString(),
    };

    private static string Localizacao(LogAssinaturaEpiItem item) => item.LocalizacaoStatus switch
    {
        StatusLocalizacaoAssinatura.Capturada when item.Latitude is { } lat && item.Longitude is { } lon =>
            string.Create(CultureInfo.InvariantCulture, $"{lat:0.0000}, {lon:0.0000}")
                + (item.PrecisaoMetros is { } p ? string.Create(CultureInfo.InvariantCulture, $" (±{p:0} m)") : ""),
        StatusLocalizacaoAssinatura.NaoAutorizada => "não autorizada pelo usuário",
        StatusLocalizacaoAssinatura.Indisponivel => "indisponível no aparelho",
        _ => "não capturado (anterior à implantação)",
    };

    // Navegador e sistema, em português simples, a partir do User-Agent.
    internal static string Equipamento(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "não registrado (anterior à implantação)";
        var ua = userAgent;
        var navegador =
            ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge" :
            ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox" :
            ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome" :
            ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : "navegador";
        var sistema =
            ua.Contains("Android", StringComparison.Ordinal) ? "Android" :
            ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) ? "iOS" :
            ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" :
            ua.Contains("Mac OS", StringComparison.Ordinal) ? "macOS" :
            ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : "sistema não identificado";
        var celular = ua.Contains("Mobile", StringComparison.Ordinal) ? " (celular)" : "";
        return $"{navegador} · {sistema}{celular}";
    }

    private static bool EhImagem(byte[] bytes)
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
}
