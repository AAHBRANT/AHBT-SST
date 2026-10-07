using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Resumo do DDS enviado ao Telegram quando o DDS é encerrado: quantas presenças (por facial e por
// digital), quantas falhas do facial e quem precisa refazer o cadastro. Só matrícula, quantidades e
// obra: sem nome, sem CPF e sem foto. Chamado pelo controller depois do encerramento, em melhor esforço.
public record EnviarResumoDdsTelegramCommand(Guid DdsId) : IRequest;

public class EnviarResumoDdsTelegramCommandHandler : IRequestHandler<EnviarResumoDdsTelegramCommand>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ITelegramResumoService _telegram;

    public EnviarResumoDdsTelegramCommandHandler(IAppDbContext db, IMediator mediator, ITelegramResumoService telegram)
    {
        _db = db;
        _mediator = mediator;
        _telegram = telegram;
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

        await _telegram.EnviarAsync(Montar(obraNome, ConverterParaBrasilia(DateTime.UtcNow), tipos.Count, tipos.Count + pendentes, facial, digital, falhas,
            fracos.Select(f => (f.Matricula, f.Falhas)).ToList()), ct);
    }

    internal static string Montar(string obra, DateTime quando, int presencas, int total, int facial, int digital, int falhas, IReadOnlyList<(string? Matricula, int Falhas)> fracos)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DDS encerrado — {obra}");
        sb.AppendLine(quando.ToString("dd/MM/yyyy, HH:mm"));
        sb.AppendLine();
        sb.AppendLine($"Presenças: {presencas} de {total}");
        sb.AppendLine($" · Facial: {facial}");
        sb.AppendLine($" · Digital: {digital}");
        sb.AppendLine();
        sb.AppendLine($"Falhas do facial: {falhas}");
        if (fracos.Count > 0)
        {
            sb.AppendLine("Revisar o cadastro (3 ou mais falhas):");
            foreach (var f in fracos)
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
