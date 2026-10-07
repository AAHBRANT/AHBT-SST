using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Resumo do DDS enviado ao Telegram quando o DDS é encerrado: quantas presenças (por facial e por
// digital), quantas falhas do facial e quem precisa refazer o cadastro. Vai como IMAGEM no padrão AAHBRANT
// (IImagemResumoDdsService) com uma legenda curta; se a imagem não puder ser gerada ou enviada, cai para
// o resumo em texto, para o aviso nunca deixar de chegar. Só matrícula, quantidades e obra: sem nome, sem
// CPF e sem foto. Chamado pelo controller depois do encerramento, em melhor esforço.
public record EnviarResumoDdsTelegramCommand(Guid DdsId) : IRequest;

public class EnviarResumoDdsTelegramCommandHandler : IRequestHandler<EnviarResumoDdsTelegramCommand>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ITelegramResumoService _telegram;
    private readonly IImagemResumoDdsService _imagem;
    private readonly ILogger<EnviarResumoDdsTelegramCommandHandler> _logger;

    public EnviarResumoDdsTelegramCommandHandler(
        IAppDbContext db,
        IMediator mediator,
        ITelegramResumoService telegram,
        IImagemResumoDdsService imagem,
        ILogger<EnviarResumoDdsTelegramCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _telegram = telegram;
        _imagem = imagem;
        _logger = logger;
    }

    public async Task Handle(EnviarResumoDdsTelegramCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.FirstOrDefaultAsync(d => d.Id == request.DdsId, ct);
        if (dds is null) return;

        var obraNome = await _db.Obras.Where(o => o.Id == dds.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? "Obra";

        var tipos = await _db.DdsParticipantes
            .Where(p => p.DdsId == dds.Id)
            .Select(p => p.FotoTipo)
            .ToListAsync(ct);
        var facial = tipos.Count(t => t == TipoFotoParticipante.Facial);
        var digital = tipos.Count(t => t == TipoFotoParticipante.Biometria);
        var pendentes = await _db.DdsFuncionariosSelecionados.CountAsync(s => s.DdsId == dds.Id, ct);

        var falhas = await _db.FalhasReconhecimentoFacial
            .CountAsync(f => f.ObraId == dds.ObraId && f.OcorridaEm >= dds.CreatedAtUtc, ct);

        var fracos = await _mediator.Send(new ListarCadastrosFaciaisFracosQuery(dds.ObraId), ct);

        var dados = new ResumoDdsDados(
            obraNome, ConverterParaBrasilia(DateTime.UtcNow), tipos.Count, tipos.Count + pendentes, facial, digital, falhas,
            fracos.Select(f => (f.Matricula, f.Falhas)).ToList());

        if (await TentarEnviarImagemAsync(dados, ct)) return;
        await _telegram.EnviarAsync(Montar(dados), ct);
    }

    private async Task<bool> TentarEnviarImagemAsync(ResumoDdsDados dados, CancellationToken ct)
    {
        try
        {
            var png = _imagem.Gerar(dados);
            return await _telegram.EnviarImagemAsync(png, Legenda(dados), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível enviar a imagem do resumo do DDS ao Telegram; enviando em texto.");
            return false;
        }
    }

    public static string Legenda(ResumoDdsDados d) =>
        $"DDS encerrado, {d.Obra}. {d.Presencas} de {d.Total} presenças, {d.Falhas} {(d.Falhas == 1 ? "falha" : "falhas")} do facial.";

    public static string Montar(ResumoDdsDados d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DDS encerrado — {d.Obra}");
        sb.AppendLine(d.Quando.ToString("dd/MM/yyyy, HH:mm"));
        sb.AppendLine();
        sb.AppendLine($"Presenças: {d.Presencas} de {d.Total}");
        sb.AppendLine($" · Facial: {d.Facial}");
        sb.AppendLine($" · Digital: {d.Digital}");
        sb.AppendLine();
        sb.AppendLine($"Falhas do facial: {d.Falhas}");
        if (d.Fracos.Count > 0)
        {
            sb.AppendLine("Revisar o cadastro (3 ou mais falhas):");
            foreach (var f in d.Fracos)
                sb.AppendLine($" · Mat. {(string.IsNullOrWhiteSpace(f.Matricula) ? "sem matrícula" : f.Matricula)} ({f.Falhas})");
        }
        return sb.ToString().TrimEnd();
    }

    private static DateTime ConverterParaBrasilia(DateTime utc)
    {
        TimeZoneInfo fuso;
        try { fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { fuso = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), fuso);
    }
}
