using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Inspecoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Inspecoes.EnvioEmail;

// Envio do relatório da inspeção por e-mail (03/11/2026, "tipo um Autentique"): o usuário marca os
// contatos salvos (padrão da obra + os seus), pode digitar outros e salvá-los, e envia o PDF assinado
// pela caixa sst@aahbrant.com. Só inspeção Concluída (já assinada) — relatório pela metade não sai.

public record ContatoEmailDto(Guid Id, string? Nome, string Email, bool PadraoObra);

public record EnvioEmailInspecaoDto(Guid Id, DateTime EnviadoEmUtc, string EnviadoPorNome, List<string> Destinatarios, List<string> Falhas, string? Mensagem);

public record EnvioEmailInspecaoPainelDto(bool PodeEnviar, List<ContatoEmailDto> Contatos, List<EnvioEmailInspecaoDto> Envios);

internal static class UsuarioAtual
{
    public static Task<Usuario?> ObterAsync(IAppDbContext db, string? azureAdObjectId, CancellationToken ct) =>
        string.IsNullOrEmpty(azureAdObjectId)
            ? Task.FromResult<Usuario?>(null)
            : db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == azureAdObjectId, ct);
}

public record ObterEnvioEmailInspecaoQuery(Guid InspecaoId, string? AzureAdObjectId) : IRequest<EnvioEmailInspecaoPainelDto?>;

public class ObterEnvioEmailInspecaoQueryHandler : IRequestHandler<ObterEnvioEmailInspecaoQuery, EnvioEmailInspecaoPainelDto?>
{
    private readonly IAppDbContext _db;

    public ObterEnvioEmailInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EnvioEmailInspecaoPainelDto?> Handle(ObterEnvioEmailInspecaoQuery request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes.AsNoTracking()
            .Where(i => i.Id == request.InspecaoId)
            .Select(i => new { i.ObraId, i.Status })
            .FirstOrDefaultAsync(ct);
        if (inspecao is null) return null;

        var usuario = await UsuarioAtual.ObterAsync(_db, request.AzureAdObjectId, ct);
        var usuarioId = usuario?.Id;

        var contatos = await _db.ContatosEmailRelatorio.AsNoTracking()
            .Where(c => c.ObraId == inspecao.ObraId || (usuarioId != null && c.UsuarioId == usuarioId))
            .OrderByDescending(c => c.ObraId != null).ThenBy(c => c.Nome ?? c.Email)
            .Select(c => new ContatoEmailDto(c.Id, c.Nome, c.Email, c.ObraId != null))
            .ToListAsync(ct);

        var envios = (await _db.EnviosEmailInspecao.AsNoTracking()
                .Where(e => e.InspecaoId == request.InspecaoId)
                .OrderByDescending(e => e.EnviadoEmUtc)
                .ToListAsync(ct))
            .Select(e => new EnvioEmailInspecaoDto(e.Id, e.EnviadoEmUtc, e.EnviadoPorNome,
                Separar(e.Destinatarios), Separar(e.Falhas), e.Mensagem))
            .ToList();

        return new EnvioEmailInspecaoPainelDto(inspecao.Status == StatusInspecao.Concluida, contatos, envios);
    }

    private static List<string> Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? new List<string>() : valor.Split("; ", StringSplitOptions.RemoveEmptyEntries).ToList();
}

public record EnviarEmailInspecaoCommand(
    Guid InspecaoId,
    List<string> Emails,
    string? Mensagem,
    List<string> SalvarEmails,
    bool SalvarNaObra,
    string? AzureAdObjectId,
    string? NomeRemetente) : IRequest<EnvioEmailInspecaoDto>;

public class EnviarEmailInspecaoCommandValidator : AbstractValidator<EnviarEmailInspecaoCommand>
{
    public const int MaximoDestinatarios = 20;

    public EnviarEmailInspecaoCommandValidator()
    {
        RuleFor(x => x.Emails).NotEmpty().WithMessage("Selecione ao menos um destinatário.")
            .Must(e => e.Count <= MaximoDestinatarios).WithMessage($"No máximo {MaximoDestinatarios} destinatários por envio.");
        RuleForEach(x => x.Emails).NotEmpty().EmailAddress().WithMessage("E-mail inválido: {PropertyValue}.").MaximumLength(200);
        RuleForEach(x => x.SalvarEmails).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Mensagem).MaximumLength(2000);
    }
}

public class EnviarEmailInspecaoCommandHandler : IRequestHandler<EnviarEmailInspecaoCommand, EnvioEmailInspecaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IEmailService _email;
    private readonly ILogger<EnviarEmailInspecaoCommandHandler> _logger;

    public EnviarEmailInspecaoCommandHandler(IAppDbContext db, IMediator mediator, IEmailService email, ILogger<EnviarEmailInspecaoCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _email = email;
        _logger = logger;
    }

    public async Task<EnvioEmailInspecaoDto> Handle(EnviarEmailInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes.Include(i => i.Obra)
            .FirstOrDefaultAsync(i => i.Id == request.InspecaoId, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.InspecaoId} não encontrada.");
        if (inspecao.Status != StatusInspecao.Concluida)
            throw new InvalidOperationException("Só é possível enviar o relatório de uma inspeção encerrada e assinada.");

        var usuario = await UsuarioAtual.ObterAsync(_db, request.AzureAdObjectId, ct);
        var nomeRemetente = usuario?.Nome ?? request.NomeRemetente ?? "Equipe de SST";

        var emails = request.Emails.Select(Normalizar).Distinct().ToList();
        await SalvarContatosAsync(request, inspecao.ObraId, usuario?.Id, ct);

        var pdf = await _mediator.Send(new ExportarInspecaoPdfQuery(inspecao.Id), ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.InspecaoId} não encontrada.");
        var numero = inspecao.NumeroDocumento ?? inspecao.Id.ToString()[..8];
        var obraNome = inspecao.Obra?.Nome ?? "—";
        var assunto = $"Relatório de inspeção {numero} — Obra {obraNome}";
        var corpo = CorpoEmail(numero, obraNome, inspecao.Data, nomeRemetente, request.Mensagem);
        var anexo = new[] { new AnexoEmail($"Inspecao-{numero}.pdf", "application/pdf", pdf) };

        // Um envio por destinatário (IEmailService manda para um só), assim um e-mail recusado não
        // derruba os outros e o histórico diz exatamente quem recebeu.
        var enviados = new List<string>();
        var falhas = new List<string>();
        foreach (var para in emails)
        {
            try
            {
                await _email.EnviarAsync(para, assunto, corpo, anexo, ct);
                enviados.Add(para);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao enviar o relatório da inspeção {InspecaoId} para {Email}", inspecao.Id, para);
                falhas.Add(para);
            }
        }

        var envio = new EnvioEmailInspecao
        {
            InspecaoId = inspecao.Id,
            EnviadoEmUtc = DateTime.UtcNow,
            EnviadoPorNome = nomeRemetente,
            Destinatarios = string.Join("; ", enviados),
            Falhas = falhas.Count > 0 ? string.Join("; ", falhas) : null,
            Mensagem = string.IsNullOrWhiteSpace(request.Mensagem) ? null : request.Mensagem.Trim(),
        };
        _db.EnviosEmailInspecao.Add(envio);
        await _db.SaveChangesAsync(ct);

        if (enviados.Count == 0)
            throw new InvalidOperationException("Nenhum e-mail foi enviado. Verifique os endereços e tente novamente.");

        return new EnvioEmailInspecaoDto(envio.Id, envio.EnviadoEmUtc, envio.EnviadoPorNome, enviados, falhas, envio.Mensagem);
    }

    private async Task SalvarContatosAsync(EnviarEmailInspecaoCommand request, Guid obraId, Guid? usuarioId, CancellationToken ct)
    {
        var salvar = request.SalvarEmails.Select(Normalizar).Distinct().ToList();
        if (salvar.Count == 0) return;
        // Sem usuário identificado (dev sem Entra) não há "Meus contatos": o e-mail vai para a obra.
        var naObra = request.SalvarNaObra || usuarioId is null;

        var existentes = await _db.ContatosEmailRelatorio
            .Where(c => salvar.Contains(c.Email) && (naObra ? c.ObraId == obraId : c.UsuarioId == usuarioId))
            .Select(c => c.Email)
            .ToListAsync(ct);
        foreach (var email in salvar.Except(existentes))
        {
            _db.ContatosEmailRelatorio.Add(new ContatoEmailRelatorio
            {
                Email = email,
                ObraId = naObra ? obraId : null,
                UsuarioId = naObra ? null : usuarioId,
            });
        }
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();

    private static string CorpoEmail(string numero, string obraNome, DateTime data, string remetente, string? mensagem)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        const string Just = "text-align:justify;";
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · Inspeções</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">Relatório de inspeção {H(numero)}</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;{Just}\">");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Prezado(a),</p>");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Encaminhamos o relatório da inspeção {H(numero)}, realizada na obra {H(obraNome)} em {data:dd/MM/yyyy}, encerrada e assinada eletronicamente. O documento completo, com as evidências fotográficas e a validação por QR Code, segue em anexo.</p>");
        if (!string.IsNullOrWhiteSpace(mensagem))
            sb.Append($"<p style=\"margin:0 0 16px;white-space:pre-line;{Just}\">{H(mensagem.Trim())}</p>");
        sb.Append($"<p style=\"margin:0;{Just}\">Atenciosamente,<br>{H(remetente)}<br>AAHBRANT Engenharia &amp; Construções</p>");
        sb.Append("</td></tr></table></td></tr></table></body></html>");
        return sb.ToString();
    }
}

public record RemoverContatoEmailCommand(Guid ContatoId, string? AzureAdObjectId) : IRequest;

public class RemoverContatoEmailCommandHandler : IRequestHandler<RemoverContatoEmailCommand>
{
    private readonly IAppDbContext _db;

    public RemoverContatoEmailCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverContatoEmailCommand request, CancellationToken ct)
    {
        var contato = await _db.ContatosEmailRelatorio.FirstOrDefaultAsync(c => c.Id == request.ContatoId, ct)
            ?? throw new KeyNotFoundException($"Contato {request.ContatoId} não encontrado.");
        if (contato.UsuarioId is not null)
        {
            var usuario = await UsuarioAtual.ObterAsync(_db, request.AzureAdObjectId, ct);
            if (usuario?.Id != contato.UsuarioId)
                throw new KeyNotFoundException($"Contato {request.ContatoId} não encontrado.");
        }
        contato.Ativo = false;
        await _db.SaveChangesAsync(ct);
    }
}
