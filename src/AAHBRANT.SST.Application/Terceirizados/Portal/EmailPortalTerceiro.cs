using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// E-mails do portal de terceiros, no mesmo layout institucional de AvisoDocumentosVencidos (logo
// inline, faixa vinho #670000).
public static class EmailPortalTerceiro
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string AssuntoConvite(string numeroContrato, string obraNome) =>
        $"[SST AAHBRANT] Acesso ao portal de terceiros — Contrato {numeroContrato} · Obra {obraNome}";

    public static string HtmlConvite(string empresaNome, string numeroContrato, string obraNome, string link, DateTime expiraEmUtc) =>
        Montar("Portal de terceiros", $"Contrato {numeroContrato} — Obra {obraNome}", sb =>
        {
            sb.Append($"<p style=\"margin:0 0 16px;\">Olá, equipe {H(empresaNome)}.</p>");
            sb.Append("<p style=\"margin:0 0 16px;\">Pelo portal de terceiros da AAHBRANT, a sua empresa cadastra as pessoas que vão trabalhar ")
              .Append("neste contrato e envia o ASO e os certificados de treinamento de cada uma. A equipe de Segurança do Trabalho ")
              .Append("confere cada documento antes da liberação para a obra.</p>");
            sb.Append($"<p style=\"margin:24px 0;text-align:center;\"><a href=\"{H(link)}\" style=\"background:#670000;color:#ffffff;text-decoration:none;padding:12px 28px;font-weight:600;display:inline-block;\">Acessar o portal</a></p>");
            sb.Append("<p style=\"margin:0 0 12px;font-size:13px;color:#5d5d5d;\">Ao abrir o link, enviaremos um código de confirmação para este mesmo e-mail. ")
              .Append($"O link é exclusivo deste contrato e vale até {expiraEmUtc:dd/MM/yyyy}. Não o repasse a pessoas de fora da sua empresa.</p>");
        });

    public const string AssuntoCodigo = "[SST AAHBRANT] Código de acesso ao portal de terceiros";

    public static string HtmlCodigo(string codigo) =>
        Montar("Portal de terceiros", "Código de acesso", sb =>
        {
            sb.Append("<p style=\"margin:0 0 16px;\">Use o código abaixo para entrar no portal de terceiros da AAHBRANT:</p>");
            sb.Append($"<p style=\"margin:24px 0;text-align:center;font-size:32px;letter-spacing:.3em;font-weight:700;color:#670000;\">{H(codigo)}</p>");
            sb.Append($"<p style=\"margin:0 0 12px;font-size:13px;color:#5d5d5d;\">O código vale por {(int)PortalTerceiroRegras.ValidadeCodigo.TotalMinutes} minutos. ")
              .Append("Se você não tentou acessar o portal, ignore esta mensagem.</p>");
        });

    public static string AssuntoRecusa(string nomePessoa) =>
        $"[SST AAHBRANT] Documento devolvido — {nomePessoa}";

    public static string HtmlRecusa(string nomePessoa, string tipoDocumento, string motivo) =>
        Montar("Portal de terceiros", "Documento devolvido para correção", sb =>
        {
            sb.Append($"<p style=\"margin:0 0 16px;\">A equipe de Segurança do Trabalho devolveu o <b>{H(tipoDocumento)}</b> de <b>{H(nomePessoa)}</b>.</p>");
            sb.Append($"<div style=\"background:#fdf5df;border-left:4px solid #670000;padding:12px 14px;margin:0 0 20px;\"><b>Motivo:</b> {H(motivo)}</div>");
            sb.Append("<p style=\"margin:0 0 12px;\">Envie o documento corrigido pelo portal, usando o mesmo link de acesso do contrato. ")
              .Append("A pessoa só é liberada para a obra depois que todos os documentos forem aprovados.</p>");
        });

    private static string Montar(string sobretitulo, string titulo, Action<StringBuilder> corpo)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append($"<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · {H(sobretitulo)}</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">{H(titulo)}</div></td></tr>");
        sb.Append("<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;\">");
        corpo(sb);
        sb.Append("</td></tr>");
        sb.Append("<tr><td style=\"padding:14px 28px;border-top:1px solid #e2ded6;font-size:11px;color:#8a8a8a;\">Mensagem automática do sistema de SST da AAHBRANT. Não responda este e-mail.</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }
}
