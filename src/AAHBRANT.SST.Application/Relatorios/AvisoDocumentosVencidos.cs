using System.Net;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Relatorios;

// Aviso diário de PGR/PCMSO vencido (pedido do usuário, 16/10/2026): de segunda a sexta, a partir das 07:00
// (Brasília), cada responsável recebe UM e-mail e UM aviso no sininho do Teams com todos os documentos vencidos
// das obras dele, até o documento ser regularizado. Quem recebe: Técnico de Segurança, Engenheiro de Segurança e
// Gestor de Obra vinculados à obra (UsuarioPerfilObra) + Engenheiro de Segurança da empresa (perfil sem obra).
// Obra concluída/encerrada não gera aviso. Mesmo esquema de idempotência da lista de presença: o Worker pergunta a
// cada poucos minutos e ExecucaoRelatorioAgendado garante um disparo por dia, mesmo se o Worker voltar às 10:00.
public record EnviarAvisoDocumentosVencidosCommand(DateTime? AgoraBrasilia = null) : IRequest<int>;

public record DocumentoVencido(
    string Tipo, Guid DocumentoId, string Nome, Guid? ObraId, string ObraNome, DateTime Vencimento, int DiasVencido,
    string EntidadeOrigemTipo, IReadOnlyList<DivergenciaLeituraIa> Divergencias);

public class EnviarAvisoDocumentosVencidosCommandHandler : IRequestHandler<EnviarAvisoDocumentosVencidosCommand, int>
{
    public static readonly TimeSpan Horario = new(7, 0, 0);

    internal static readonly TipoPerfilAcesso[] PerfisDaObra =
        { TipoPerfilAcesso.TecnicoSeguranca, TipoPerfilAcesso.EngenheiroSeguranca, TipoPerfilAcesso.GestorDeObra };

    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly IFilaNotificacaoTeams _filaTeams;
    private readonly ILogger<EnviarAvisoDocumentosVencidosCommandHandler> _logger;

    public EnviarAvisoDocumentosVencidosCommandHandler(
        IAppDbContext db, IEmailService email, IFilaNotificacaoTeams filaTeams,
        ILogger<EnviarAvisoDocumentosVencidosCommandHandler> logger)
    {
        _db = db;
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

        var documentos = await ListarDocumentosVencidosAsync(_db, hoje, ct);
        var porUsuario = await AgruparPorResponsavelAsync(_db, documentos, ct);

        var enviados = 0;
        foreach (var (usuario, docs) in porUsuario)
        {
            var ordenados = docs.OrderByDescending(d => d.DiasVencido).ToList();
            try
            {
                await _email.EnviarAsync(usuario.Email, Assunto(ordenados), MontarHtml(usuario.Nome, ordenados), ct);
                enviados++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aviso de documentos vencidos: falha ao enviar e-mail para {Email}.", usuario.Email);
            }

            await EnfileirarSininhoAsync(usuario.Id, ordenados, ct);
        }

        execucao.RelatoriosGerados = enviados;
        await _db.SaveChangesAsync(ct);
        return enviados;
    }

    public static async Task<List<DocumentoVencido>> ListarDocumentosVencidosAsync(IAppDbContext db, DateTime hoje, CancellationToken ct)
    {
        var obrasInativas = new[] { StatusObra.Concluida, StatusObra.Encerrada };

        var pgrs = await db.Pgrs
            .Where(p => p.Status != StatusPgr.Encerrado && p.DataTermino != null && p.DataTermino < hoje)
            .Where(p => !obrasInativas.Contains(p.Obra!.Status))
            .Select(p => new { p.Id, p.Nome, p.ObraId, ObraNome = p.Obra!.Nome, Vencimento = p.DataTermino!.Value })
            .ToListAsync(ct);

        var pcmsos = await db.PcmsoDetalhes
            .Where(p => p.Validade != null && p.Validade < hoje)
            .Where(p => p.Status != StatusPcmsoDocumento.Rascunho
                && p.Status != StatusPcmsoDocumento.Obsoleto
                && p.Status != StatusPcmsoDocumento.Cancelado)
            .Where(p => p.ObraId == null || !obrasInativas.Contains(p.Obra!.Status))
            .Select(p => new { p.Id, p.Nome, p.ObraId, ObraNome = p.Obra != null ? p.Obra.Nome : null, Vencimento = p.Validade!.Value })
            .ToListAsync(ct);

        var ids = pgrs.Select(p => p.Id).Concat(pcmsos.Select(p => p.Id)).ToList();
        var divergencias = await UltimasDivergenciasAsync(db, ids, ct);
        IReadOnlyList<DivergenciaLeituraIa> Div(Guid id) => divergencias.GetValueOrDefault(id) ?? new List<DivergenciaLeituraIa>();

        return pgrs
            .Select(p => new DocumentoVencido("PGR", p.Id, p.Nome, p.ObraId, p.ObraNome, p.Vencimento,
                (hoje - p.Vencimento.Date).Days, "PgrTermino", Div(p.Id)))
            .Concat(pcmsos.Select(p => new DocumentoVencido("PCMSO", p.Id, p.Nome, p.ObraId, p.ObraNome ?? "Sem obra vinculada",
                p.Vencimento, (hoje - p.Vencimento.Date).Days, PcmsoAlertaProvider.EntidadeOrigem, Div(p.Id))))
            .ToList();
    }

    // Divergências da leitura com IA mais recente de cada documento (só as que já viraram cadastro ou estão prontas).
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

    public static async Task<Dictionary<Usuario, List<DocumentoVencido>>> AgruparPorResponsavelAsync(
        IAppDbContext db, List<DocumentoVencido> documentos, CancellationToken ct)
    {
        var resultado = new Dictionary<Usuario, List<DocumentoVencido>>();
        if (documentos.Count == 0) return resultado;

        var obraIds = documentos.Where(d => d.ObraId.HasValue).Select(d => d.ObraId!.Value).Distinct().ToList();
        var vinculos = await db.UsuariosPerfilObra
            .Include(v => v.Usuario)
            .Where(v => v.Usuario!.Status == StatusUsuario.Ativo && v.Usuario.Email != "")
            .Where(v =>
                (v.ObraId != null && obraIds.Contains(v.ObraId.Value) && PerfisDaObra.Contains(v.PerfilAcesso!.Tipo!.Value))
                || (v.ObraId == null && v.PerfilAcesso!.Tipo == TipoPerfilAcesso.EngenheiroSeguranca))
            .ToListAsync(ct);

        var porId = new Dictionary<Guid, Usuario>();
        foreach (var doc in documentos)
        {
            var responsaveis = vinculos
                .Where(v => v.ObraId == null || v.ObraId == doc.ObraId)
                .Select(v => v.Usuario!)
                .DistinctBy(u => u.Id);
            foreach (var usuario in responsaveis)
            {
                var chave = porId.TryGetValue(usuario.Id, out var existente) ? existente : porId[usuario.Id] = usuario;
                if (!resultado.TryGetValue(chave, out var lista)) resultado[chave] = lista = new List<DocumentoVencido>();
                lista.Add(doc);
            }
        }
        return resultado;
    }

    // O sininho do Teams exige um Alerta real (histórico de envio aponta para ele): usa o alerta "vencido" em aberto
    // que o Motor Central já criou para o documento mais atrasado. Sem alerta ainda (motor roda a cada poucas horas),
    // a pessoa recebe só o e-mail hoje.
    private async Task EnfileirarSininhoAsync(Guid usuarioId, List<DocumentoVencido> docs, CancellationToken ct)
    {
        var chaves = docs.Select(d => d.DocumentoId).ToList();
        var alertas = await _db.Alertas
            .Where(a => chaves.Contains(a.EntidadeOrigemId))
            .Where(a => a.Status != StatusAlerta.Resolvido)
            .Select(a => new { a.Id, a.EntidadeOrigemTipo, a.EntidadeOrigemId })
            .ToListAsync(ct);

        var alerta = docs
            .Select(d => alertas.FirstOrDefault(a => a.EntidadeOrigemId == d.DocumentoId && a.EntidadeOrigemTipo == d.EntidadeOrigemTipo))
            .FirstOrDefault(a => a is not null);
        if (alerta is null) return;

        var titulo = docs.Count == 1
            ? $"VENCIDO: {docs[0].Tipo} da obra {docs[0].ObraNome} (há {docs[0].DiasVencido} dia(s))"
            : $"{docs.Count} documentos de SST vencidos nas suas obras";
        var descricao = string.Join("; ", docs.Select(d => $"{d.Tipo} {d.ObraNome} — venceu em {d.Vencimento:dd/MM/yyyy}"));
        await _filaTeams.EnfileirarAsync(new NotificacaoTeamsMensagem(alerta.Id, usuarioId, titulo, descricao), ct);
    }

    public static string Assunto(List<DocumentoVencido> docs) => docs.Count == 1
        ? $"[SST] {docs[0].Tipo} vencido — Obra {docs[0].ObraNome} (há {docs[0].DiasVencido} dia(s))"
        : $"[SST] {docs.Count} documentos vencidos nas suas obras";

    public static string MontarHtml(string nomeDestinatario, List<DocumentoVencido> docs)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        const string Just = "text-align:justify;";
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        var tituloTopo = docs.Count == 1 ? $"{docs[0].Tipo} vencido — Obra {docs[0].ObraNome}" : $"{docs.Count} documentos de SST vencidos";
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · Alerta de documento legal</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">{H(tituloTopo)}</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;{Just}\">");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Olá, {H(nomeDestinatario)}.</p>");
        sb.Append($"<div style=\"background:#fde8e8;border-left:4px solid #670000;padding:12px 14px;margin:0 0 20px;{Just}\">");
        sb.Append(docs.Count == 1
            ? $"<b>O {H(docs[0].Tipo)} desta obra está vencido desde {docs[0].Vencimento:dd/MM/yyyy} (há {docs[0].DiasVencido} dia(s)).</b> "
            : "<b>Há documentos de SST vencidos nas obras sob sua responsabilidade.</b> ");
        sb.Append("A obra fica sem programa vigente, em desacordo com a NR-01 (PGR) e a NR-07 (PCMSO). Providencie a nova revisão com o responsável técnico.</div>");

        foreach (var d in docs)
        {
            sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 8px;font-size:13px;\">");
            void Linha(string rotulo, string valor) =>
                sb.Append($"<tr><td style=\"padding:7px 0;border-bottom:1px solid #e2ded6;color:#5d5d5d;width:42%;\">{rotulo}</td><td style=\"padding:7px 0;border-bottom:1px solid #e2ded6;\">{valor}</td></tr>");
            Linha("Documento", $"<b>{H(d.Tipo)}</b> · {H(d.Nome)}");
            Linha("Obra", H(d.ObraNome));
            Linha(d.Tipo == "PGR" ? "Fim da vigência" : "Validade", $"{d.Vencimento:dd/MM/yyyy} <span style=\"color:#670000;font-weight:600;\">(há {d.DiasVencido} dia(s))</span>");
            sb.Append("</table>");

            if (d.Divergencias.Count > 0)
            {
                sb.Append($"<div style=\"font-size:14px;font-weight:600;color:#670000;margin:14px 0 8px;\">Divergências encontradas na leitura ({d.Divergencias.Count})</div>");
                foreach (var div in d.Divergencias)
                {
                    var fundo = div.Gravidade == "alta" ? "#fde8e8" : "#fdf5df";
                    sb.Append($"<div style=\"background:{fundo};padding:8px 10px;margin:0 0 6px;font-size:13px;{Just}\">{H(div.Texto)}</div>");
                }
            }
            sb.Append("<div style=\"height:14px;\"></div>");
        }

        sb.Append("<a href=\"{{LINK_SST}}\" style=\"display:inline-block;background:#670000;color:#ffffff;text-decoration:none;padding:11px 22px;font-weight:600;font-size:14px;\">Abrir o SST no Teams</a>");
        sb.Append($"<p style=\"font-size:12px;color:#5d5d5d;margin:20px 0 0;{Just}\">Você recebe este aviso por estar vinculado à obra como Técnico de Segurança, Engenheiro de Segurança ou Gestor, ou por ser Engenheiro de Segurança da empresa. O aviso se repete de segunda a sexta, às 7h, até o documento ser atualizado no SST.</p>");
        sb.Append("</td></tr>");
        sb.Append($"<tr><td style=\"background:#ebe9ad;padding:14px 28px;font-size:11px;color:#4a4a2a;{Just}\">AAHBRANT Engenharia &amp; Construções · Mensagem automática do sistema SST. Não responda este e-mail.</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }
}

// Prévia (só leitura) do próximo disparo do aviso diário: quem recebe, por quê e quais documentos. Tela em
// Alertas › Prévia do aviso (pedido do usuário, 19/10/2026), para conferir antes de ligar
// AvisoDocumentosVencidos:Habilitado no Worker.
public record PreviaAvisoDocumentosVencidosQuery(DateTime? AgoraBrasilia = null) : IRequest<PreviaAvisoDocumentosVencidosDto>;

public record PreviaAvisoDocumentoDto(string Tipo, string Nome, string Obra, DateTime Vencimento, int DiasVencido, int Divergencias);
public record PreviaAvisoDestinatarioDto(
    string Nome, string Email, List<string> Motivos, bool RecebeSininho, string? MotivoSemSininho,
    List<PreviaAvisoDocumentoDto> Documentos);
public record PreviaAvisoDocumentosVencidosDto(DateTime DataEnvio, List<PreviaAvisoDestinatarioDto> Destinatarios);

public class PreviaAvisoDocumentosVencidosQueryHandler
    : IRequestHandler<PreviaAvisoDocumentosVencidosQuery, PreviaAvisoDocumentosVencidosDto>
{
    private readonly IAppDbContext _db;

    public PreviaAvisoDocumentosVencidosQueryHandler(IAppDbContext db) => _db = db;

    // Próximo dia útil em que o aviso sai às 07:00: hoje, se ainda não deu 07:00 num dia útil; senão o próximo dia útil.
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
        var documentos = await EnviarAvisoDocumentosVencidosCommandHandler.ListarDocumentosVencidosAsync(_db, dataEnvio, ct);
        var porUsuario = await EnviarAvisoDocumentosVencidosCommandHandler.AgruparPorResponsavelAsync(_db, documentos, ct);
        if (porUsuario.Count == 0) return new PreviaAvisoDocumentosVencidosDto(dataEnvio, new());

        // Por que cada pessoa recebe (perfil + obra), para conferir vínculos antigos ou faltando.
        var usuarioIds = porUsuario.Keys.Select(u => u.Id).ToList();
        var obraIds = documentos.Where(d => d.ObraId.HasValue).Select(d => d.ObraId!.Value).Distinct().ToList();
        var vinculos = await _db.UsuariosPerfilObra
            .Where(v => usuarioIds.Contains(v.UsuarioId))
            .Where(v =>
                (v.ObraId != null && obraIds.Contains(v.ObraId.Value)
                    && EnviarAvisoDocumentosVencidosCommandHandler.PerfisDaObra.Contains(v.PerfilAcesso!.Tipo!.Value))
                || (v.ObraId == null && v.PerfilAcesso!.Tipo == TipoPerfilAcesso.EngenheiroSeguranca))
            .Select(v => new { v.UsuarioId, Tipo = v.PerfilAcesso!.Tipo, ObraNome = v.Obra != null ? v.Obra.Nome : null })
            .ToListAsync(ct);

        // Mesma regra do envio: o sininho só sai se o Motor já abriu um alerta para algum dos documentos.
        var docIds = documentos.Select(d => d.DocumentoId).ToList();
        var alertas = await _db.Alertas
            .Where(a => docIds.Contains(a.EntidadeOrigemId) && a.Status != StatusAlerta.Resolvido)
            .Select(a => new { a.EntidadeOrigemTipo, a.EntidadeOrigemId })
            .ToListAsync(ct);
        bool TemAlerta(DocumentoVencido d) =>
            alertas.Any(a => a.EntidadeOrigemId == d.DocumentoId && a.EntidadeOrigemTipo == d.EntidadeOrigemTipo);

        var destinatarios = porUsuario
            .Select(kv =>
            {
                var motivos = vinculos
                    .Where(v => v.UsuarioId == kv.Key.Id)
                    .Select(v => v.ObraNome is null
                        ? $"{RotuloPerfil(v.Tipo)} da empresa (todas as obras)"
                        : $"{RotuloPerfil(v.Tipo)} — {v.ObraNome}")
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();
                var semSininho = kv.Value.Any(TemAlerta) ? null : "Motor de Alertas ainda não abriu o alerta destes documentos";
                return new PreviaAvisoDestinatarioDto(kv.Key.Nome, kv.Key.Email, motivos, semSininho is null, semSininho, kv.Value
                    .OrderByDescending(d => d.DiasVencido)
                    .Select(d => new PreviaAvisoDocumentoDto(d.Tipo, d.Nome, d.ObraNome, d.Vencimento, d.DiasVencido, d.Divergencias.Count))
                    .ToList());
            })
            .OrderBy(d => d.Nome)
            .ToList();

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
