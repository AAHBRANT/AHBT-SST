using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Relatorios;

// Aviso diário de itens de SST vencidos (pedido do usuário, 16/10/2026 para PGR/PCMSO; ampliado em 19/10/2026 para
// todos os itens vencidos da obra — ASO, exames da função, treinamentos, EPI, ações de plano, extintores e
// equipamentos — e passado para as 08:00). De segunda a sexta, a partir das 08:00 (Brasília), cada responsável recebe
// UM e-mail e UM aviso no sininho do Teams com todos os itens vencidos das obras dele, até o item ser regularizado.
// Quem recebe: Técnico de Segurança, Engenheiro de Segurança e Gestor de Obra vinculados à obra (UsuarioPerfilObra) —
// quem tem várias obras recebe as de todas num só aviso — + Engenheiro de Segurança da empresa (perfil sem obra).
// Os itens vêm dos mesmos IAlertaOrigemProvider do Motor Central de Alertas, então o aviso enxerga exatamente o que o
// motor enxerga. Obra concluída/encerrada e trabalhador desligado não geram aviso. Mesmo esquema de idempotência da
// lista de presença: o Worker pergunta a cada poucos minutos e ExecucaoRelatorioAgendado garante um disparo por dia.
public record EnviarAvisoDocumentosVencidosCommand(DateTime? AgoraBrasilia = null) : IRequest<int>;

public record ItemVencido(
    string Categoria, Guid OrigemId, string Descricao, Guid? ObraId, string ObraNome, DateTime Vencimento, int DiasVencido,
    string EntidadeOrigemTipo, IReadOnlyList<DivergenciaLeituraIa> Divergencias);

public class EnviarAvisoDocumentosVencidosCommandHandler : IRequestHandler<EnviarAvisoDocumentosVencidosCommand, int>
{
    public static readonly TimeSpan Horario = new(8, 0, 0);

    // Linhas de itens num e-mail; o resto fica para a tela do SST (o Engenheiro da empresa recebe todas as obras).
    public const int MaxItensNoEmail = 200;

    // Módulos do Motor que entram no aviso, na ordem em que aparecem no e-mail. Ficam de fora o Documento (PCMSO
    // repetido do PcmsoAlertaProvider) e o Cadastro facial (não é vencimento).
    internal static readonly (TipoModuloAlerta Modulo, string Categoria)[] Modulos =
    {
        (TipoModuloAlerta.Pgr, "PGR"),
        (TipoModuloAlerta.Pcmso, "PCMSO"),
        (TipoModuloAlerta.DocumentoTerceirizado, "Documento de terceirizado"),
        (TipoModuloAlerta.Aso, "ASO"),
        (TipoModuloAlerta.ExamesFuncao, "Exames da função"),
        (TipoModuloAlerta.Treinamento, "Treinamento"),
        (TipoModuloAlerta.Epi, "EPI"),
        (TipoModuloAlerta.PlanoAcao, "Ação de plano"),
        (TipoModuloAlerta.Extintor, "Extintor"),
        (TipoModuloAlerta.Equipamento, "Equipamento"),
    };

    // Revisão sugerida do PGR não é vencimento do programa: fica só no Motor de Alertas.
    private const string OrigemPgrRevisao = "PgrRevisao";

    internal static readonly TipoPerfilAcesso[] PerfisDaObra =
        { TipoPerfilAcesso.TecnicoSeguranca, TipoPerfilAcesso.EngenheiroSeguranca, TipoPerfilAcesso.GestorDeObra };

    // " — validade 10/10/2026" no fim do título do motor: a data já sai na coluna "Venceu em".
    private static readonly Regex SufixoData = new(@"\s—\s(validade|término da vigência)\s\d{2}/\d{2}/\d{4}$", RegexOptions.Compiled);

    private readonly IAppDbContext _db;
    private readonly IEnumerable<IAlertaOrigemProvider> _providers;
    private readonly IEmailService _email;
    private readonly IFilaNotificacaoTeams _filaTeams;
    private readonly ILogger<EnviarAvisoDocumentosVencidosCommandHandler> _logger;

    public EnviarAvisoDocumentosVencidosCommandHandler(
        IAppDbContext db, IEnumerable<IAlertaOrigemProvider> providers, IEmailService email, IFilaNotificacaoTeams filaTeams,
        ILogger<EnviarAvisoDocumentosVencidosCommandHandler> logger)
    {
        _db = db;
        _providers = providers;
        _email = email;
        _filaTeams = filaTeams;
        _logger = logger;
    }

    public async Task<int> Handle(EnviarAvisoDocumentosVencidosCommand request, CancellationToken ct)
    {
        var agora = request.AgoraBrasilia ?? FusoBrasilia.Agora();
        if (agora.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return 0;
        if (agora.TimeOfDay < Horario) return 0;

        var hoje = agora.Date;
        if (await _db.ExecucoesRelatorioAgendado.AnyAsync(e => e.Tipo == TipoRelatorio.AvisoDocumentosVencidos && e.Data == hoje, ct))
            return 0;

        // Registra o disparo antes de enviar: se duas instâncias do Worker chegarem juntas, só a que gravar
        // primeiro envia (a outra cai no índice único Tipo+Data).
        var execucao = new ExecucaoRelatorioAgendado
        {
            Tipo = TipoRelatorio.AvisoDocumentosVencidos,
            Data = hoje,
            ExecutadoEm = DateTime.UtcNow,
        };
        _db.ExecucoesRelatorioAgendado.Add(execucao);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return 0;
        }

        var itens = await ListarItensVencidosAsync(_db, _providers, hoje, ct);
        var porUsuario = await AgruparPorResponsavelAsync(_db, itens, ct);

        var enviados = 0;
        foreach (var (usuario, doUsuario) in porUsuario)
        {
            var ordenados = doUsuario.OrderByDescending(d => d.DiasVencido).ToList();
            try
            {
                await _email.EnviarAsync(usuario.Email, Assunto(ordenados), MontarHtml(usuario.Nome, ordenados), ct);
                enviados++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aviso de itens vencidos: falha ao enviar e-mail para {Email}.", usuario.Email);
            }

            await EnfileirarSininhoAsync(usuario.Id, ordenados, ct);
        }

        execucao.RelatoriosGerados = enviados;
        await _db.SaveChangesAsync(ct);
        return enviados;
    }

    public static async Task<List<ItemVencido>> ListarItensVencidosAsync(
        IAppDbContext db, IEnumerable<IAlertaOrigemProvider> providers, DateTime hoje, CancellationToken ct)
    {
        var categorias = Modulos.ToDictionary(m => m.Modulo, m => m.Categoria);
        var brutos = new List<(string Categoria, AlertaOrigemItem Item)>();
        foreach (var provider in providers.Where(p => categorias.ContainsKey(p.Modulo)))
        {
            foreach (var item in await provider.ObterItensAsync(ct))
            {
                if (item.Substituido || item.DataVencimento.Date >= hoje || item.EntidadeOrigemTipo == OrigemPgrRevisao) continue;
                brutos.Add((categorias[provider.Modulo], item));
            }
        }
        if (brutos.Count == 0) return new();

        var obraIds = brutos.Where(b => b.Item.ObraId.HasValue).Select(b => b.Item.ObraId!.Value).Distinct().ToList();
        var obrasInativas = new[] { StatusObra.Concluida, StatusObra.Encerrada };
        var obrasAtivas = await db.Obras
            .Where(o => obraIds.Contains(o.Id) && !obrasInativas.Contains(o.Status))
            .Select(o => new { o.Id, o.Nome })
            .ToDictionaryAsync(o => o.Id, o => o.Nome, ct);

        var trabalhadorIds = brutos.Where(b => b.Item.TrabalhadorId.HasValue).Select(b => b.Item.TrabalhadorId!.Value).Distinct().ToList();
        var desligados = (await db.Trabalhadores
            .Where(t => trabalhadorIds.Contains(t.Id))
            .Where(t => t.Situacao == SituacaoTrabalhador.Desligado || (t.DataDemissao != null && t.DataDemissao <= hoje))
            .Select(t => t.Id)
            .ToListAsync(ct)).ToHashSet();

        var validos = brutos
            .Where(b => !b.Item.ObraId.HasValue || obrasAtivas.ContainsKey(b.Item.ObraId.Value))
            .Where(b => !b.Item.TrabalhadorId.HasValue || !desligados.Contains(b.Item.TrabalhadorId.Value))
            .ToList();

        var idsDocumentos = validos.Where(b => b.Categoria is "PGR" or "PCMSO").Select(b => b.Item.EntidadeOrigemId).ToList();
        var divergencias = await UltimasDivergenciasAsync(db, idsDocumentos, ct);

        return validos
            .Select(b => new ItemVencido(
                b.Categoria,
                b.Item.EntidadeOrigemId,
                SufixoData.Replace(b.Item.Titulo, string.Empty),
                b.Item.ObraId,
                b.Item.ObraId.HasValue ? obrasAtivas[b.Item.ObraId.Value] : "Sem obra vinculada",
                b.Item.DataVencimento.Date,
                (hoje - b.Item.DataVencimento.Date).Days,
                b.Item.EntidadeOrigemTipo,
                (IReadOnlyList<DivergenciaLeituraIa>?)divergencias.GetValueOrDefault(b.Item.EntidadeOrigemId) ?? Array.Empty<DivergenciaLeituraIa>()))
            .ToList();
    }

    // Divergências da leitura com IA mais recente de cada PGR/PCMSO (só as que já viraram cadastro ou estão prontas).
    private static async Task<Dictionary<Guid, List<DivergenciaLeituraIa>>> UltimasDivergenciasAsync(IAppDbContext db, List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        var leituras = await db.LeiturasDocumentoIa
            .Where(l => ids.Contains(l.DocumentoId) && l.Status == StatusLeituraIa.Concluida && l.ResultadoJson != null)
            .Select(l => new { l.DocumentoId, l.CreatedAtUtc, l.ResultadoJson })
            .ToListAsync(ct);

        var resultado = new Dictionary<Guid, List<DivergenciaLeituraIa>>();
        foreach (var leitura in leituras.GroupBy(l => l.DocumentoId).Select(g => g.OrderByDescending(l => l.CreatedAtUtc).First()))
        {
            try
            {
                var lida = JsonSerializer.Deserialize<ResultadoLeituraIa>(leitura.ResultadoJson!, ProcessadorLeituraIa.Json);
                if (lida?.Divergencias is { Count: > 0 } d) resultado[leitura.DocumentoId] = d;
            }
            catch (JsonException)
            {
                // Leitura gravada num formato antigo: o aviso sai sem as divergências.
            }
        }
        return resultado;
    }

    public static async Task<Dictionary<Usuario, List<ItemVencido>>> AgruparPorResponsavelAsync(
        IAppDbContext db, List<ItemVencido> itens, CancellationToken ct)
    {
        var resultado = new Dictionary<Usuario, List<ItemVencido>>();
        if (itens.Count == 0) return resultado;

        var obraIds = itens.Where(d => d.ObraId.HasValue).Select(d => d.ObraId!.Value).Distinct().ToList();
        var vinculos = await db.UsuariosPerfilObra
            .Include(v => v.Usuario)
            .Where(v => v.Usuario!.Status == StatusUsuario.Ativo && v.Usuario.Email != "")
            .Where(v =>
                (v.ObraId != null && obraIds.Contains(v.ObraId.Value) && PerfisDaObra.Contains(v.PerfilAcesso!.Tipo!.Value))
                || (v.ObraId == null && v.PerfilAcesso!.Tipo == TipoPerfilAcesso.EngenheiroSeguranca))
            .ToListAsync(ct);

        var porId = new Dictionary<Guid, Usuario>();
        foreach (var item in itens)
        {
            var responsaveis = vinculos
                .Where(v => v.ObraId == null || v.ObraId == item.ObraId)
                .Select(v => v.Usuario!)
                .DistinctBy(u => u.Id);
            foreach (var usuario in responsaveis)
            {
                var chave = porId.TryGetValue(usuario.Id, out var existente) ? existente : porId[usuario.Id] = usuario;
                if (!resultado.TryGetValue(chave, out var lista)) resultado[chave] = lista = new List<ItemVencido>();
                lista.Add(item);
            }
        }
        return resultado;
    }

    // O sininho do Teams exige um Alerta real (histórico de envio aponta para ele): usa o alerta "vencido" em aberto
    // que o Motor Central já criou para o item mais atrasado da pessoa. O motor cria alerta para todo item vencido,
    // então só fica sem sininho quem tem apenas itens que venceram depois da última passada do motor.
    private async Task EnfileirarSininhoAsync(Guid usuarioId, List<ItemVencido> itens, CancellationToken ct)
    {
        var alertaId = await AlertaParaSininhoAsync(_db, itens, ct);
        if (alertaId is null) return;

        var titulo = itens.Count == 1
            ? $"VENCIDO: {itens[0].Categoria} — {itens[0].ObraNome} (há {itens[0].DiasVencido} dia(s))"
            : $"{itens.Count} itens de SST vencidos nas suas obras";
        await _filaTeams.EnfileirarAsync(new NotificacaoTeamsMensagem(alertaId.Value, usuarioId, titulo, ResumoPorCategoria(itens)), ct);
    }

    public static async Task<Guid?> AlertaParaSininhoAsync(IAppDbContext db, List<ItemVencido> itens, CancellationToken ct)
    {
        var chaves = itens.Select(d => d.OrigemId).Distinct().ToList();
        var alertas = await db.Alertas
            .Where(a => chaves.Contains(a.EntidadeOrigemId))
            .Where(a => a.Status != StatusAlerta.Resolvido)
            .Select(a => new { a.Id, a.EntidadeOrigemTipo, a.EntidadeOrigemId })
            .ToListAsync(ct);

        return itens
            .OrderByDescending(d => d.DiasVencido)
            .Select(d => alertas.FirstOrDefault(a => a.EntidadeOrigemId == d.OrigemId && a.EntidadeOrigemTipo == d.EntidadeOrigemTipo)?.Id)
            .FirstOrDefault(id => id is not null);
    }

    // "ASO: 3 · Treinamento: 12 · PGR: 1", na ordem de Modulos.
    public static string ResumoPorCategoria(List<ItemVencido> itens) => string.Join(" · ", Modulos
        .Select(m => (m.Categoria, Quantidade: itens.Count(i => i.Categoria == m.Categoria)))
        .Where(c => c.Quantidade > 0)
        .Select(c => $"{c.Categoria}: {c.Quantidade}"));

    public static string Assunto(List<ItemVencido> itens) => itens.Count == 1
        ? $"[SST] {itens[0].Categoria} vencido — Obra {itens[0].ObraNome} (há {itens[0].DiasVencido} dia(s))"
        : $"[SST] {itens.Count} itens vencidos nas suas obras";

    public static string MontarHtml(string nomeDestinatario, List<ItemVencido> itens)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        const string Just = "text-align:justify;";
        const string Celula = "padding:7px 6px;border-bottom:1px solid #e2ded6;vertical-align:top;";
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        var tituloTopo = itens.Count == 1 ? $"{itens[0].Categoria} vencido — Obra {itens[0].ObraNome}" : $"{itens.Count} itens de SST vencidos";
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · Itens vencidos nas suas obras</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">{H(tituloTopo)}</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;{Just}\">");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Olá, {H(nomeDestinatario)}.</p>");
        sb.Append($"<div style=\"background:#fde8e8;border-left:4px solid #670000;padding:12px 14px;margin:0 0 20px;{Just}\">");
        sb.Append("<b>Há itens de SST vencidos nas obras sob sua responsabilidade.</b> ");
        sb.Append("Itens vencidos deixam a obra em desacordo com as Normas Regulamentadoras aplicáveis. Regularize e atualize o cadastro no SST.</div>");

        // Resumo por categoria.
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 22px;font-size:13px;\">");
        foreach (var (_, categoria) in Modulos)
        {
            var quantidade = itens.Count(i => i.Categoria == categoria);
            if (quantidade == 0) continue;
            sb.Append($"<tr><td style=\"{Celula}color:#5d5d5d;\">{H(categoria)}</td><td style=\"{Celula}text-align:right;font-weight:600;color:#670000;\">{quantidade}</td></tr>");
        }
        sb.Append("</table>");

        // Itens por obra, do mais atrasado ao menos atrasado.
        var restantes = MaxItensNoEmail;
        foreach (var obra in itens.GroupBy(i => i.ObraNome).OrderBy(g => g.Key))
        {
            if (restantes <= 0) break;
            sb.Append($"<div style=\"font-size:15px;font-weight:600;color:#670000;margin:18px 0 6px;\">Obra {H(obra.Key)} <span style=\"color:#5d5d5d;font-weight:400;font-size:13px;\">· {obra.Count()} item(ns)</span></div>");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;font-size:13px;\">");
            sb.Append($"<tr style=\"color:#5d5d5d;font-size:12px;\"><td style=\"{Celula}width:22%;\">Tipo</td><td style=\"{Celula}\">Item</td><td style=\"{Celula}width:18%;\">Venceu em</td><td style=\"{Celula}width:14%;text-align:right;\">Atraso</td></tr>");
            foreach (var i in obra.OrderByDescending(i => i.DiasVencido).Take(restantes))
            {
                restantes--;
                sb.Append($"<tr><td style=\"{Celula}font-weight:600;\">{H(i.Categoria)}</td><td style=\"{Celula}\">{H(i.Descricao)}</td>");
                sb.Append($"<td style=\"{Celula}\">{i.Vencimento:dd/MM/yyyy}</td><td style=\"{Celula}text-align:right;color:#670000;font-weight:600;\">{i.DiasVencido} dia(s)</td></tr>");
                if (i.Divergencias.Count > 0)
                {
                    sb.Append($"<tr><td></td><td colspan=\"3\" style=\"padding:4px 6px 10px;\"><div style=\"font-size:12px;font-weight:600;color:#670000;margin:0 0 4px;\">Divergências encontradas na leitura ({i.Divergencias.Count})</div>");
                    foreach (var div in i.Divergencias)
                    {
                        var fundo = div.Gravidade == "alta" ? "#fde8e8" : "#fdf5df";
                        sb.Append($"<div style=\"background:{fundo};padding:6px 8px;margin:0 0 4px;font-size:12px;{Just}\">{H(div.Texto)}</div>");
                    }
                    sb.Append("</td></tr>");
                }
            }
            sb.Append("</table>");
        }
        if (itens.Count > MaxItensNoEmail)
            sb.Append($"<p style=\"margin:14px 0 0;font-size:13px;{Just}\"><b>E mais {itens.Count - MaxItensNoEmail} item(ns).</b> Veja a lista completa no SST, em Alertas.</p>");

        sb.Append("<div style=\"height:20px;\"></div>");
        sb.Append("<a href=\"{{LINK_SST}}\" style=\"display:inline-block;background:#670000;color:#ffffff;text-decoration:none;padding:11px 22px;font-weight:600;font-size:14px;\">Abrir o SST no Teams</a>");
        sb.Append($"<p style=\"font-size:12px;color:#5d5d5d;margin:20px 0 0;{Just}\">Você recebe este aviso por estar vinculado à obra como Técnico de Segurança, Engenheiro de Segurança ou Gestor, ou por ser Engenheiro de Segurança da empresa. O aviso se repete de segunda a sexta, às 8h, até o item ser regularizado no SST.</p>");
        sb.Append("</td></tr>");
        sb.Append($"<tr><td style=\"background:#ebe9ad;padding:14px 28px;font-size:11px;color:#4a4a2a;{Just}\">AAHBRANT Engenharia &amp; Construções · Mensagem automática do sistema SST. Não responda este e-mail.</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }
}

// Prévia (só leitura) do próximo disparo do aviso diário: quem recebe, por quê e quais itens. Tela em
// Alertas › Prévia do aviso (pedido do usuário, 19/10/2026), para conferir antes de ligar
// AvisoDocumentosVencidos:Habilitado no Worker.
public record PreviaAvisoDocumentosVencidosQuery(DateTime? AgoraBrasilia = null) : IRequest<PreviaAvisoDocumentosVencidosDto>;

public record PreviaAvisoItemDto(string Tipo, string Nome, string Obra, DateTime Vencimento, int DiasVencido, int Divergencias);
public record PreviaAvisoDestinatarioDto(
    string Nome, string Email, List<string> Motivos, bool RecebeSininho, string? MotivoSemSininho,
    List<PreviaAvisoItemDto> Itens);
public record PreviaAvisoDocumentosVencidosDto(DateTime DataEnvio, List<PreviaAvisoDestinatarioDto> Destinatarios);

public class PreviaAvisoDocumentosVencidosQueryHandler
    : IRequestHandler<PreviaAvisoDocumentosVencidosQuery, PreviaAvisoDocumentosVencidosDto>
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IAlertaOrigemProvider> _providers;

    public PreviaAvisoDocumentosVencidosQueryHandler(IAppDbContext db, IEnumerable<IAlertaOrigemProvider> providers)
    {
        _db = db;
        _providers = providers;
    }

    // Próximo dia útil em que o aviso sai às 08:00: hoje, se ainda não deu 08:00 num dia útil; senão o próximo dia útil.
    public static DateTime ProximoEnvio(DateTime agora)
    {
        var dia = agora.Date;
        if (agora.TimeOfDay >= EnviarAvisoDocumentosVencidosCommandHandler.Horario) dia = dia.AddDays(1);
        while (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) dia = dia.AddDays(1);
        return dia;
    }

    public async Task<PreviaAvisoDocumentosVencidosDto> Handle(PreviaAvisoDocumentosVencidosQuery request, CancellationToken ct)
    {
        var dataEnvio = ProximoEnvio(request.AgoraBrasilia ?? FusoBrasilia.Agora());
        var itens = await EnviarAvisoDocumentosVencidosCommandHandler.ListarItensVencidosAsync(_db, _providers, dataEnvio, ct);
        var porUsuario = await EnviarAvisoDocumentosVencidosCommandHandler.AgruparPorResponsavelAsync(_db, itens, ct);
        if (porUsuario.Count == 0) return new PreviaAvisoDocumentosVencidosDto(dataEnvio, new());

        // Por que cada pessoa recebe (perfil + obra), para conferir vínculos antigos ou faltando.
        var usuarioIds = porUsuario.Keys.Select(u => u.Id).ToList();
        var obraIds = itens.Where(d => d.ObraId.HasValue).Select(d => d.ObraId!.Value).Distinct().ToList();
        var vinculos = await _db.UsuariosPerfilObra
            .Where(v => usuarioIds.Contains(v.UsuarioId))
            .Where(v =>
                (v.ObraId != null && obraIds.Contains(v.ObraId.Value)
                    && EnviarAvisoDocumentosVencidosCommandHandler.PerfisDaObra.Contains(v.PerfilAcesso!.Tipo!.Value))
                || (v.ObraId == null && v.PerfilAcesso!.Tipo == TipoPerfilAcesso.EngenheiroSeguranca))
            .Select(v => new { v.UsuarioId, Tipo = v.PerfilAcesso!.Tipo, ObraNome = v.Obra != null ? v.Obra.Nome : null })
            .ToListAsync(ct);

        var destinatarios = new List<PreviaAvisoDestinatarioDto>();
        foreach (var (usuario, doUsuario) in porUsuario.OrderBy(kv => kv.Key.Nome))
        {
            var motivos = vinculos
                .Where(v => v.UsuarioId == usuario.Id)
                .Select(v => v.ObraNome is null
                    ? $"{RotuloPerfil(v.Tipo)} da empresa (todas as obras)"
                    : $"{RotuloPerfil(v.Tipo)} — {v.ObraNome}")
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            // Mesma regra do envio: o sininho sai se o Motor já abriu um alerta para algum dos itens.
            var temSininho = await EnviarAvisoDocumentosVencidosCommandHandler.AlertaParaSininhoAsync(_db, doUsuario, ct) is not null;
            destinatarios.Add(new PreviaAvisoDestinatarioDto(
                usuario.Nome, usuario.Email, motivos, temSininho,
                temSininho ? null : "Motor de Alertas ainda não abriu o alerta destes itens",
                doUsuario
                    .OrderByDescending(d => d.DiasVencido)
                    .Select(d => new PreviaAvisoItemDto(d.Categoria, d.Descricao, d.ObraNome, d.Vencimento, d.DiasVencido, d.Divergencias.Count))
                    .ToList()));
        }

        return new PreviaAvisoDocumentosVencidosDto(dataEnvio, destinatarios);
    }

    private static string RotuloPerfil(TipoPerfilAcesso? tipo) => tipo switch
    {
        TipoPerfilAcesso.TecnicoSeguranca => "Técnico de Segurança",
        TipoPerfilAcesso.EngenheiroSeguranca => "Engenheiro de Segurança",
        TipoPerfilAcesso.GestorDeObra => "Gestor de Obra",
        _ => tipo?.ToString() ?? "Perfil",
    };
}
