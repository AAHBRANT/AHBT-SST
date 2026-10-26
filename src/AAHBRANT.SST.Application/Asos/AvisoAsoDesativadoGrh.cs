using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Alertas.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Asos;

// Aviso ao Técnico de Segurança da obra quando a sincronização do G-RH desativa ASO que sumiu da origem (pedido do
// usuário, 26/10/2026): um e-mail e um aviso no sininho do Teams por técnico, com as pessoas da obra dele e, para
// cada ASO, só a data e se estava apto. Informa também se a pessoa continua com outro ASO apto válido, que é o que
// decide se ela ficou bloqueada ou se não há nada a fazer.
public record AsoDesativadoItem(
    Guid ObraId, string Obra, string Pessoa, string? Funcao, DateTime DataExame, DateTime DataValidade, string Resultado,
    bool ContinuaComAsoValido);

public static class AvisoAsoDesativadoGrh
{
    public const string EntidadeOrigemTipo = "AsoDesativadoGrh";

    // Linhas de pessoas num e-mail; o resto fica para a tela do SST.
    public const int MaxItensNoEmail = 100;

    public static async Task EnviarAsync(
        IAppDbContext db, IEmailService email, IMediator mediator, ILogger logger,
        IReadOnlyList<Aso> desativados, DateTime hoje, CancellationToken ct)
    {
        if (desativados.Count == 0) return;

        var itens = await MontarItensAsync(db, desativados, hoje, ct);
        if (itens.Count == 0) return;

        var obraIds = itens.Select(i => i.ObraId).Distinct().ToList();
        var vinculos = await db.UsuariosPerfilObra
            .Include(v => v.Usuario)
            .Where(v => v.ObraId != null && obraIds.Contains(v.ObraId.Value) && v.PerfilAcesso!.Tipo == TipoPerfilAcesso.TecnicoSeguranca)
            .Where(v => v.Usuario!.Status == StatusUsuario.Ativo && v.Usuario.Email != "")
            .ToListAsync(ct);

        foreach (var obra in itens.GroupBy(i => i.ObraId))
        {
            if (!vinculos.Any(v => v.ObraId == obra.Key))
                logger.LogWarning("ASO desativado por sumir do G-RH: a obra {Obra} não tem Técnico de Segurança para avisar.", obra.First().Obra);
        }

        foreach (var tecnico in vinculos.GroupBy(v => v.UsuarioId))
        {
            var usuario = tecnico.First().Usuario!;
            var obrasDele = tecnico.Select(v => v.ObraId!.Value).ToHashSet();
            var doTecnico = itens.Where(i => obrasDele.Contains(i.ObraId)).ToList();
            if (doTecnico.Count == 0) continue;

            try
            {
                await email.EnviarAsync(usuario.Email, Assunto(doTecnico), MontarHtml(usuario.Nome, doTecnico), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ASO desativado por sumir do G-RH: falha ao enviar e-mail para {Email}.", usuario.Email);
            }

            try
            {
                await mediator.Send(new CriarAlertaCommand(
                    TipoAlerta.AsoDesativadoGrh, SeveridadeAlerta.Atencao, Titulo(doTecnico), Resumo(doTecnico),
                    EntidadeOrigemTipo, Guid.NewGuid(), TrabalhadorId: null,
                    ObraId: doTecnico.Select(i => i.ObraId).Distinct().Count() == 1 ? doTecnico[0].ObraId : null,
                    DestinatarioUsuarioId: usuario.Id, DataLimiteTratamento: null), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ASO desativado por sumir do G-RH: falha ao criar o aviso do sininho para {Email}.", usuario.Email);
            }
        }
    }

    // Nome, função e obra de quem perdeu o ASO, e se continua com outro ASO apto dentro da validade. Trabalhador
    // desligado não gera aviso.
    internal static async Task<List<AsoDesativadoItem>> MontarItensAsync(
        IAppDbContext db, IReadOnlyList<Aso> desativados, DateTime hoje, CancellationToken ct)
    {
        var trabalhadorIds = desativados.Select(a => a.TrabalhadorId).Distinct().ToList();
        var trabalhadores = await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => trabalhadorIds.Contains(t.Id))
            .Where(t => t.Situacao != SituacaoTrabalhador.Desligado && (t.DataDemissao == null || t.DataDemissao > hoje))
            .Select(t => new { t.Id, t.Nome, t.ObraId, t.FuncaoId })
            .ToListAsync(ct);
        if (trabalhadores.Count == 0) return new();

        var obraIds = trabalhadores.Select(t => t.ObraId).Distinct().ToList();
        var funcaoIds = trabalhadores.Select(t => t.FuncaoId).Distinct().ToList();
        var obras = await db.Obras.IgnoreQueryFilters()
            .Where(o => obraIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var funcoes = await db.Funcoes.IgnoreQueryFilters()
            .Where(f => funcaoIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var idsComAsoValido = (await db.Asos
                .Where(a => trabalhadorIds.Contains(a.TrabalhadorId))
                .Where(a => a.ResultadoStatus == ResultadoAso.Apto || a.ResultadoStatus == ResultadoAso.AptoComRestricao)
                .Select(a => new { a.TrabalhadorId, a.DataValidade })
                .ToListAsync(ct))
            .Where(a => a.DataValidade.Date >= hoje)
            .Select(a => a.TrabalhadorId)
            .ToHashSet();

        var itens = new List<AsoDesativadoItem>();
        foreach (var aso in desativados)
        {
            var t = trabalhadores.FirstOrDefault(x => x.Id == aso.TrabalhadorId);
            if (t is null) continue;
            itens.Add(new AsoDesativadoItem(
                t.ObraId, obras.GetValueOrDefault(t.ObraId, "Sem obra"), t.Nome, funcoes.GetValueOrDefault(t.FuncaoId),
                aso.DataExame.Date, aso.DataValidade.Date, RotuloResultado(aso.ResultadoStatus), idsComAsoValido.Contains(aso.TrabalhadorId)));
        }
        return itens;
    }

    // Só o que importa para a decisão: estava apto ou não.
    internal static string RotuloResultado(ResultadoAso resultado) => resultado switch
    {
        ResultadoAso.Apto or ResultadoAso.AptoComRestricao => "Apto",
        ResultadoAso.Inapto => "Inapto",
        _ => "Sem resultado",
    };

    public static string Titulo(IReadOnlyList<AsoDesativadoItem> itens)
    {
        var obras = itens.Select(i => i.Obra).Distinct().ToList();
        var escopo = obras.Count == 1 ? $"Obra {obras[0]}" : $"{obras.Count} obras";
        var texto = itens.Count == 1
            ? $"ASO de {itens[0].Pessoa} saiu do G-RH e foi desativado — {escopo}"
            : $"{itens.Count} ASOs saíram do G-RH e foram desativados — {escopo}";
        return texto.Length <= 200 ? texto : texto[..199] + "…";
    }

    public static string Resumo(IReadOnlyList<AsoDesativadoItem> itens)
    {
        var bloqueadas = itens.Count(i => !i.ContinuaComAsoValido);
        return bloqueadas == 0
            ? "Todas as pessoas continuam com outro ASO apto válido."
            : $"{bloqueadas} pessoa(s) ficaram sem ASO apto válido e estão bloqueadas.";
    }

    public static string Assunto(IReadOnlyList<AsoDesativadoItem> itens) => itens.Count == 1
        ? $"[SST] ASO de {itens[0].Pessoa} desativado — Obra {itens[0].Obra}"
        : $"[SST] {itens.Count} ASOs desativados por sumirem do G-RH";

    public static string MontarHtml(string nomeDestinatario, IReadOnlyList<AsoDesativadoItem> itens)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        const string Just = "text-align:justify;";
        const string Celula = "padding:7px 6px;border-bottom:1px solid #e2ded6;vertical-align:top;";
        var bloqueadas = itens.Count(i => !i.ContinuaComAsoValido);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · Sincronização com o G-RH</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">{H(itens.Count == 1 ? "1 ASO desativado" : $"{itens.Count} ASOs desativados")}</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;{Just}\">");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Olá, {H(nomeDestinatario)}.</p>");
        var fundo = bloqueadas > 0 ? "#fde8e8" : "#fdf5df";
        sb.Append($"<div style=\"background:{fundo};border-left:4px solid #670000;padding:12px 14px;margin:0 0 20px;{Just}\">");
        sb.Append("<b>Os ASOs abaixo deixaram de existir no G-RH e foram desativados no SST.</b> ");
        sb.Append(bloqueadas > 0
            ? $"{bloqueadas} pessoa(s) ficaram sem ASO apto válido e estão bloqueadas até o ASO ser lançado de novo. Confira no G-RH."
            : "Todas as pessoas continuam com outro ASO apto válido, então nada precisa ser feito.");
        sb.Append("</div>");

        var restantes = MaxItensNoEmail;
        foreach (var obra in itens.GroupBy(i => i.Obra).OrderBy(g => g.Key))
        {
            if (restantes <= 0) break;
            sb.Append($"<div style=\"font-size:15px;font-weight:600;color:#670000;margin:18px 0 6px;\">Obra {H(obra.Key)} <span style=\"color:#5d5d5d;font-weight:400;font-size:13px;\">· {obra.Count()} ASO(s)</span></div>");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;font-size:13px;\">");
            sb.Append($"<tr style=\"color:#5d5d5d;font-size:12px;\"><td style=\"{Celula}\">Pessoa</td><td style=\"{Celula}width:16%;\">Exame</td><td style=\"{Celula}width:16%;\">Validade</td><td style=\"{Celula}width:14%;\">Resultado</td><td style=\"{Celula}width:22%;\">Hoje</td></tr>");
            foreach (var i in obra.OrderBy(i => i.Pessoa).Take(restantes))
            {
                restantes--;
                var situacao = i.ContinuaComAsoValido
                    ? "<span style=\"color:#2f6b3a;font-weight:600;\">Tem outro ASO válido</span>"
                    : "<span style=\"color:#670000;font-weight:600;\">Sem ASO válido</span>";
                var funcao = string.IsNullOrWhiteSpace(i.Funcao) ? string.Empty : $"<div style=\"color:#5d5d5d;font-size:12px;\">{H(i.Funcao)}</div>";
                sb.Append($"<tr><td style=\"{Celula}\"><b>{H(i.Pessoa)}</b>{funcao}</td><td style=\"{Celula}\">{i.DataExame:dd/MM/yyyy}</td><td style=\"{Celula}\">{i.DataValidade:dd/MM/yyyy}</td>");
                sb.Append($"<td style=\"{Celula}\">{H(i.Resultado)}</td><td style=\"{Celula}\">{situacao}</td></tr>");
            }
            sb.Append("</table>");
        }
        if (itens.Count > MaxItensNoEmail)
            sb.Append($"<p style=\"margin:14px 0 0;font-size:13px;{Just}\"><b>E mais {itens.Count - MaxItensNoEmail} ASO(s).</b> Veja a lista completa no SST.</p>");

        sb.Append("<div style=\"height:20px;\"></div>");
        sb.Append("<a href=\"{{LINK_SST}}\" style=\"display:inline-block;background:#670000;color:#ffffff;text-decoration:none;padding:11px 22px;font-weight:600;font-size:14px;\">Abrir o SST no Teams</a>");
        sb.Append($"<p style=\"font-size:12px;color:#5d5d5d;margin:20px 0 0;{Just}\">Você recebe este aviso por estar vinculado à obra como Técnico de Segurança. O SST desativa o ASO que some do G-RH; se o documento voltar a existir lá, o ASO é reativado sozinho na rodada seguinte.</p>");
        sb.Append("</td></tr>");
        sb.Append($"<tr><td style=\"background:#ebe9ad;padding:14px 28px;font-size:11px;color:#4a4a2a;{Just}\">AAHBRANT Engenharia &amp; Construções · Mensagem automática do sistema SST. Não responda este e-mail.</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }
}
