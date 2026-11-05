using System.Globalization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Documentos;

// Certificado de Assinaturas Eletrônicas — modelo único para todo documento com assinatura (05/11).
// Nasceu na Ficha de EPI (06/10, no estilo do certificado do DocuSign). Sem página de resumo separada:
// a 1ª página traz o cabeçalho do certificado, cada assinatura tem sua evidência com o resumo no topo e o
// QR de validação pública dela, e a última fecha com os eventos do documento. Em listas grandes
// (Compacto) vão duas assinaturas por página, com imagens menores.
public static class CertificadoAssinaturasPdf
{
    private const string CorMarca = "#670000";
    private const string CorAzul = "#0f5ba8";
    private const string CorAzulFundo = "#eef4fb";
    private const string CorBarra = "#e8e5de";
    private const string CorLinha = "#d9d3c9";
    private const string CorSuave = "#6f6666";
    private const string CorFundoSuave = "#f5f3ee";

    public static void Adicionar(IDocumentContainer documento, CertificadoAssinaturasModelo modelo)
    {
        if (modelo.Itens.Count == 0) return;

        // Fluxo contínuo: cabeçalho do certificado, as evidências e os eventos do documento. Normal = uma
        // assinatura por página; compacto = quantas couberem, sem nunca partir uma assinatura ao meio.
        documento.Page(pagina =>
        {
            ConfigurarPagina(pagina, modelo);
            pagina.Content().PaddingVertical(8).Column(coluna =>
            {
                coluna.Spacing(7);
                CabecalhoCertificado(coluna, modelo);
                for (var i = 0; i < modelo.Itens.Count; i++)
                {
                    var item = modelo.Itens[i];
                    var numero = i + 1;
                    if (i > 0)
                    {
                        if (modelo.Compacto) coluna.Item().PaddingVertical(2).LineHorizontal(0.8f).LineColor(CorLinha);
                        else coluna.Item().PageBreak();
                    }
                    coluna.Item().ShowEntire().Column(c => Evidencias(c, modelo, item, numero));
                }
                coluna.Item().ShowEntire().Column(c => EventosDocumento(c, modelo));
            });
        });
    }

    private static void ConfigurarPagina(PageDescriptor pagina, CertificadoAssinaturasModelo modelo)
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(2, Unit.Centimetre);
        pagina.DefaultTextStyle(estilo => estilo.FontSize(8.5f));
        pagina.Header().Column(coluna => CabecalhoDocumentoPadrao.Desenhar(
            coluna, $"{modelo.NomeDocumento} — Certificado de Assinaturas", modelo.ObraNome, modelo.ObraLogoConteudo));
        pagina.Footer().Column(coluna => RodapeDocumentoPadrao.Desenhar(
            coluna, modelo.NomeDocumento, protocolo: null, null, modelo.ConteudoHash, modelo.UrlValidacaoPublica, modelo.QrCodePng, temAssinatura: false));
    }

    private static void CabecalhoCertificado(ColumnDescriptor coluna, CertificadoAssinaturasModelo modelo)
    {
        coluna.Item().Text("Certificado de Assinaturas Eletrônicas").FontSize(13).Bold().FontColor(CorMarca);
        coluna.Item().Text(modelo.Subtitulo).FontSize(9).FontColor(CorSuave);
        coluna.Item().Element(c => Barra(c, "Resumo"));
        coluna.Item().Row(l =>
        {
            l.Spacing(10);
            l.RelativeItem(3).Element(x => Campo(x, "ID do registro", modelo.IdRegistro));
            l.RelativeItem(2).Column(c =>
            {
                c.Item().Element(x => Campo(x, "Situação", modelo.Pendente ? "Pendente" : "Concluído"));
                c.Item().Element(x => Campo(x, "Assinaturas", modelo.Itens.Count.ToString(CultureInfo.InvariantCulture)));
            });
            l.RelativeItem(3).Column(c =>
            {
                c.Item().Element(x => Campo(x, "Emitido em", Data(modelo.EmitidoEmUtc) + " (Brasília, UTC-3)"));
                c.Item().Element(x => Campo(x, "Origem", modelo.Origem));
            });
        });
        coluna.Item().Element(x => Campo(x, "Hash SHA-256 do documento", modelo.ConteudoHash));
        if (modelo.PainelGeral is { } geral)
            coluna.Item().Element(c => PainelHorizontal(c, geral));
    }

    // Painel geral em faixa (seções lado a lado), para caber no resumo da 1ª página.
    private static void PainelHorizontal(IContainer container, PainelCertificado painel)
    {
        container.Border(0.6f).BorderColor(CorLinha).Background("#fffefb").Padding(6).Column(c =>
        {
            c.Item().Text(t =>
            {
                t.Span(painel.Titulo).FontSize(10).Bold().FontColor(CorMarca);
                if (!string.IsNullOrWhiteSpace(painel.Subtitulo)) t.Span($"  ·  {painel.Subtitulo}").FontSize(7.5f).FontColor(CorSuave);
            });
            c.Item().PaddingTop(3).Row(r =>
            {
                r.Spacing(12);
                foreach (var secao in painel.Secoes)
                    r.RelativeItem().Column(s =>
                    {
                        s.Item().Text(secao.Titulo.ToUpperInvariant()).FontSize(6.5f).Bold().FontColor(CorMarca);
                        foreach (var linha in secao.Linhas ?? new())
                            s.Item().Text(t => { t.Span(linha.Rotulo + ": ").FontSize(7).SemiBold().FontColor(CorSuave); t.Span(linha.Valor).FontSize(7); });
                    });
            });
        });
    }

    private static void EventosDocumento(ColumnDescriptor coluna, CertificadoAssinaturasModelo modelo)
    {
        coluna.Item().PaddingTop(4).Element(c => Barra(c, "Eventos do documento"));
        coluna.Item().Element(c => LinhaDocumento(c, "Log gerado a partir dos registros do sistema", "Registrado", Data(modelo.EmitidoEmUtc)));
        coluna.Item().Element(c => LinhaDocumento(c, "Assinaturas conferidas", "Integridade verificada (SHA-256 acima)", Data(modelo.EmitidoEmUtc)));
    }

    private static void LinhaDocumento(IContainer container, string evento, string estado, string data) =>
        container.BorderBottom(0.5f).BorderColor(CorLinha).PaddingVertical(3).Row(r =>
        {
            r.RelativeItem(4).Text(evento).SemiBold().FontSize(8);
            r.RelativeItem(4).Text(estado).FontSize(8);
            r.RelativeItem(2.4f).Text(data).FontSize(8);
        });

    // ───────────────────────────── Evidências de uma assinatura ─────────────────────────────

    private static void Evidencias(ColumnDescriptor coluna, CertificadoAssinaturasModelo modelo, CertificadoAssinaturaItem item, int numero)
    {
        var ev = item.Evidencia;
        var compacto = modelo.Compacto;
        coluna.Spacing(compacto ? 4 : 7);

        coluna.Item().Row(topo =>
        {
            topo.Spacing(10);
            topo.RelativeItem().Column(cab =>
            {
                cab.Spacing(compacto ? 2 : 4);
                cab.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Evidências da assinatura {numero} de {modelo.Itens.Count}").FontSize(compacto ? 10 : 12).Bold().FontColor(CorMarca);
                    r.AutoItem().Text(Data(ev.AssinadoEmUtc)).FontSize(8).FontColor(CorSuave);
                });
                cab.Item().Text(item.Descricao).FontSize(9).SemiBold();
                cab.Item().Text(string.IsNullOrWhiteSpace(item.SignatarioDetalhe) ? item.SignatarioNome : $"{item.SignatarioNome} · {item.SignatarioDetalhe}")
                    .FontSize(8).FontColor(CorSuave);
                cab.Item().Row(r =>
                {
                    r.Spacing(10);
                    r.RelativeItem().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Nível de segurança: ").SemiBold(); t.Span(DescreverSeguranca(ev.Metodo)); });
                    if (ev.CriadoEmUtc is { } criado)
                        r.AutoItem().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Criada: ").SemiBold(); t.Span(Data(criado)); });
                });
                if (ev.Metodo is MetodoAutenticacaoAssinatura.Biometria or MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
                    cab.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Termo de aceite: ").SemiBold(); t.Span(item.TermoAceiteEm is { } a ? Data(a) : "não informado"); });
                        r.RelativeItem().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f)); t.Span("Consentimento LGPD: ").SemiBold(); t.Span(item.ConsentimentoLgpdEm is { } b ? Data(b) : "não informado"); });
                    });
            });
            if (ev.QrValidacaoPng is { Length: > 0 } qr)
            {
                var lado = compacto ? 54f : 66f;
                topo.ConstantItem(lado).Column(q =>
                {
                    q.Item().Width(lado).Height(lado).Image(qr).FitArea();
                    q.Item().AlignCenter().Text("Validar esta assinatura").FontSize(5.5f).FontColor(CorSuave).AlignCenter();
                });
            }
        });

        coluna.Item().Element(c => CaixaValidacao(c, ev, compacta: compacto));

        // Login não tem imagem: no compacto basta a caixa de validação acima, sem o quadro vazio.
        if (!(compacto && ev.Metodo == MetodoAutenticacaoAssinatura.SessaoLogada && item.Painel is null))
        coluna.Item().Row(linha =>
        {
            linha.Spacing(10);
            if (item.Painel is { } painel)
                linha.ConstantItem(compacto ? 165 : 185).Element(c => Painel(c, painel, compacto));
            linha.RelativeItem().Element(c => GradeImagens(c, ev, compacto));
        });

        coluna.Item().Element(c => DadosTecnicos(c, ev, compacto));
    }

    private static void GradeImagens(IContainer container, EvidenciaAssinaturaPdf ev, bool compacto)
    {
        var dataHora = Data(ev.AssinadoEmUtc);
        container.Border(0.5f).BorderColor(CorLinha).Column(coluna =>
        {
            if (ev.Metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
                coluna.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => CelulaImagem(c, ev.FotoCadastro, "Facial cadastrada (referência)",
                        ev.FotoCadastroEmUtc is { } d ? $"Capturada em {Data(d)}\nSHA-256 {HashCurto(ev.FotoCadastroHash) ?? "—"}" : null,
                        "Sem foto de cadastro vigente na data", dataHora, retrato34: true, compacto));
                    r.RelativeItem().Element(c => CelulaImagem(c, ev.EvidenciaAssinatura, "Facial da assinatura",
                        $"Capturada em {dataHora}\nSHA-256 {HashCurto(ev.EvidenciaAssinaturaHash) ?? "—"}",
                        "Foto não registrada (anterior à implantação)", dataHora, retrato34: true, compacto));
                });
            else if (ev.Metodo == MetodoAutenticacaoAssinatura.Biometria)
                coluna.Item().Row(r =>
                {
                    r.RelativeItem().Element(c => CelulaImagem(c, ev.DigitalCadastroImagem, "Digital cadastrada (referência)",
                        ev.DigitalCadastroEmUtc is { } d ? $"Cadastro de {Data(d)}" : null,
                        ev.DigitalCadastroEmUtc is { } d2 ? $"Cadastro de {Data(d2)} sem imagem (anterior à implantação)" : "Cadastro não localizado",
                        dataHora, retrato34: false, compacto));
                    r.RelativeItem().Element(c => CelulaImagem(c, ev.EvidenciaAssinatura, "Digital da assinatura",
                        $"Leitura em {dataHora}\nSHA-256 {HashCurto(ev.EvidenciaAssinaturaHash) ?? "—"}",
                        "Imagem não registrada (anterior à implantação)", dataHora, retrato34: false, compacto));
                });
            else
                coluna.Item().Padding(10).Text("Assinatura por sessão autenticada do usuário. Não há imagem biométrica associada.")
                    .FontSize(8).Italic().FontColor(CorSuave);
        });
    }

    private static void CelulaImagem(IContainer container, byte[]? imagem, string titulo, string? legenda, string textoAusente,
        string dataHora, bool retrato34, bool compacto)
    {
        // Facial 3×4 em pé (fotos antigas deitadas cortadas no centro só na exibição); digital em pé.
        var largura = compacto ? 72f : 104f;
        var altura = retrato34 ? largura * 4f / 3f : largura * 132f / 104f;
        if (retrato34 && imagem is { Length: > 0 } && EhImagem(imagem)) imagem = FotoRetrato34.Cortar(imagem);
        container.Padding(compacto ? 4 : 6).Column(c =>
        {
            c.Spacing(2);
            if (imagem is { Length: > 0 } && EhImagem(imagem))
                c.Item().AlignCenter().Width(largura).Height(altura).Element(x => ImagemComMarca(x, imagem, dataHora, compacto));
            else
                c.Item().AlignCenter().Width(largura).Height(altura).Border(0.6f).BorderColor(CorLinha).Background(CorFundoSuave)
                    .AlignCenter().AlignMiddle().Padding(6).Text(textoAusente).FontSize(7).FontColor(CorSuave).AlignCenter();
            c.Item().AlignCenter().Text(titulo).FontSize(7.5f).Bold();
            if (legenda is not null) c.Item().AlignCenter().Text(legenda).FontSize(6.5f).FontColor(CorSuave).AlignCenter();
        });
    }

    // Marca d'água "CONFIDENCIAL" + data e hora por cima da imagem biométrica (dado sensível — LGPD).
    private static void ImagemComMarca(IContainer container, byte[] imagem, string dataHora, bool compacto)
    {
        var branco = Color.FromARGB(215, 255, 255, 255);
        var sombra = Color.FromARGB(190, 0, 0, 0);
        var tamanho = compacto ? 9f : 12f;

        void Texto(IContainer c, Color cor) =>
            c.AlignCenter().AlignMiddle().Rotate(-28).Column(col =>
            {
                col.Item().AlignCenter().Text("CONFIDENCIAL").FontSize(tamanho).Bold().FontColor(cor);
                col.Item().AlignCenter().Text(dataHora).FontSize(compacto ? 5f : 6.5f).FontColor(cor);
            });

        container.Layers(camadas =>
        {
            camadas.Layer().Image(imagem).FitArea();
            camadas.Layer().TranslateX(0.6f).TranslateY(0.6f).Element(c => Texto(c, sombra));
            camadas.PrimaryLayer().Element(c => Texto(c, branco));
        });
    }

    private static void Painel(IContainer container, PainelCertificado painel, bool compacto)
    {
        container.Border(0.6f).BorderColor(CorLinha).Background("#fffefb").Padding(compacto ? 6 : 8).Column(c =>
        {
            c.Spacing(3);
            c.Item().AlignCenter().Text(painel.Titulo).FontSize(compacto ? 9.5f : 11).Bold().FontColor(CorMarca);
            if (!string.IsNullOrWhiteSpace(painel.Subtitulo))
                c.Item().AlignCenter().Text(painel.Subtitulo).FontSize(6.5f).FontColor(CorSuave).AlignCenter();

            foreach (var secao in painel.Secoes)
            {
                c.Item().PaddingTop(3).BorderTop(0.4f).BorderColor(CorLinha).Text(secao.Titulo.ToUpperInvariant()).FontSize(6.5f).Bold().FontColor(CorMarca);
                foreach (var linha in secao.Linhas ?? new())
                    c.Item().Row(r =>
                    {
                        r.AutoItem().Text(linha.Rotulo).FontSize(6.5f).SemiBold().FontColor(CorSuave);
                        r.ConstantItem(4);
                        r.RelativeItem().AlignRight().Text(linha.Valor).FontSize(6.5f).AlignRight();
                    });
                foreach (var it in secao.Itens ?? new())
                    c.Item().BorderTop(0.4f).BorderColor(CorLinha).PaddingVertical(3).Row(r =>
                    {
                        r.Spacing(4);
                        if (it.Foto is { Length: > 0 } foto && EhImagem(foto))
                            r.ConstantItem(30).Height(30).Border(0.4f).BorderColor(CorLinha).Image(foto).FitArea();
                        r.RelativeItem().Column(i =>
                        {
                            i.Item().Text(it.Nome).Bold().FontSize(8);
                            foreach (var d in it.Detalhes) i.Item().Text(d).FontSize(6.5f);
                        });
                    });
            }
        });
    }

    private static void DadosTecnicos(IContainer container, EvidenciaAssinaturaPdf ev, bool compacto)
    {
        container.Border(0.5f).BorderColor(CorLinha).Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Element(x => CelulaDado(x, "IP", ev.Ip ?? "não registrado", compacto));
                r.RelativeItem(1.4f).Element(x => CelulaDado(x, "Geolocalização (informada pelo aparelho)", Localizacao(ev), compacto));
                r.RelativeItem(1.2f).Element(x => CelulaDado(x, "Equipamento", Equipamento(ev.UserAgent), compacto));
            });
            c.Item().Row(r =>
            {
                r.RelativeItem().Element(x => CelulaDado(x, "Método", DescreverMetodo(ev.Metodo), compacto));
                r.RelativeItem(1.4f).Element(x => CelulaDado(x, "Leitor", ev.LeitorId is { } id ? id.ToString("N")[..8] + "…" : "não se aplica", compacto));
                r.RelativeItem(1.2f).Element(x => CelulaDado(x, "Assinada em", Data(ev.AssinadoEmUtc), compacto));
            });
        });
    }

    private static void CelulaDado(IContainer c, string rotulo, string valor, bool compacto) =>
        c.Border(0.3f).BorderColor(CorLinha).Padding(compacto ? 2.5f : 4).Column(x =>
        {
            x.Item().Text(rotulo).FontSize(6.5f).SemiBold().FontColor(CorMarca);
            x.Item().Text(valor).FontSize(compacto ? 7 : 8);
        });

    private static void CaixaValidacao(IContainer container, EvidenciaAssinaturaPdf ev, bool compacta)
    {
        var tamanho = compacta ? 6.5f : 8f;
        switch (ev.Metodo)
        {
            case MetodoAutenticacaoAssinatura.ReconhecimentoFacial:
                container.Background(CorAzulFundo).BorderLeft(3).BorderColor(CorAzul).Padding(compacta ? 3 : 6).Column(c =>
                {
                    c.Item().Text("VALIDAÇÃO MICROSOFT AZURE AI FACE").Bold().FontSize(tamanho + 0.5f).FontColor(CorAzul);
                    if (compacta)
                    {
                        c.Item().Text($"Identificação 1:N no grupo de pessoas da obra · reconhecido e aprovado · requisição {ev.ValidacaoRequisicaoId ?? "não informada"}").FontSize(tamanho);
                        return;
                    }
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Operação: ").SemiBold(); t.Span("identificação 1:N no grupo de pessoas da obra"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Modelo de reconhecimento: ").SemiBold(); t.Span(ev.ValidacaoModelo ?? "não informado"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Grupo da obra: ").SemiBold(); t.Span(ev.ValidacaoGrupoId ?? "não informado"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("Resultado: ").SemiBold(); t.Span("reconhecido e aprovado (acima do limiar exigido)"); });
                    c.Item().Text(t => { t.DefaultTextStyle(x => x.FontSize(tamanho)); t.Span("ID da requisição Azure: ").SemiBold(); t.Span(ev.ValidacaoRequisicaoId ?? "não informado"); });
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

    internal static string Data(DateTime utc) => HorarioBrasilia.De(utc).ToString("dd/MM/yyyy HH:mm:ss");

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

    private static string Localizacao(EvidenciaAssinaturaPdf ev) => ev.LocalizacaoStatus switch
    {
        StatusLocalizacaoAssinatura.Capturada when ev.Latitude is { } lat && ev.Longitude is { } lon =>
            string.Create(CultureInfo.InvariantCulture, $"{lat:0.0000}, {lon:0.0000}")
                + (ev.PrecisaoMetros is { } p ? string.Create(CultureInfo.InvariantCulture, $" (±{p:0} m)") : ""),
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

    private static bool EhImagem(byte[] bytes) => AAHBRANT.SST.Infrastructure.Imagens.ImagemSegura.EhValida(bytes);
}
