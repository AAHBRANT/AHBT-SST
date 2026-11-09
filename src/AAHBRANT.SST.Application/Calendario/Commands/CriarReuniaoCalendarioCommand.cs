using System.Net.Mail;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Calendario.Commands;

// "Quero criar reunião diretamente dentro do sistema, pelo calendário" (pedido do usuário, 09/11/2026;
// desenho aprovado em docs/mockups/2026-11-09-calendario-nova-reuniao.html). Reaproveita a mesma
// reunião do Teams da análise de acidente (IReuniaoTeamsService): o organizador é sempre quem está
// logado (AzureAdObjectId vem do token, nunca do corpo) e o Exchange envia os convites por e-mail.
public record CriarReuniaoCalendarioCommand(
    string? AzureAdObjectId,
    string Titulo,
    string? Pauta,
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<Guid> ConvidadosUsuarioIds,
    IReadOnlyList<string> EmailsExternos,
    Guid? ObraId,
    TipoRecorrenciaReuniao? Repetir = null,
    DateOnly? RepetirAte = null) : IRequest<ReuniaoCalendarioCriadaDto>;

public record ReuniaoCalendarioCriadaDto(string GraphEventId, string? LinkTeams, int TotalConvidados);

public class CriarReuniaoCalendarioCommandHandler : IRequestHandler<CriarReuniaoCalendarioCommand, ReuniaoCalendarioCriadaDto>
{
    private const int MaximoConvidados = 100;
    private readonly IAppDbContext _db;
    private readonly IReuniaoTeamsService? _reuniaoTeams;

    public CriarReuniaoCalendarioCommandHandler(IAppDbContext db, IReuniaoTeamsService? reuniaoTeams = null)
    {
        _db = db;
        _reuniaoTeams = reuniaoTeams;
    }

    public async Task<ReuniaoCalendarioCriadaDto> Handle(CriarReuniaoCalendarioCommand request, CancellationToken ct)
    {
        var titulo = (request.Titulo ?? string.Empty).Trim();
        if (titulo.Length == 0) throw new InvalidOperationException("Informe o título da reunião.");
        if (titulo.Length > 200) throw new InvalidOperationException("O título da reunião deve ter no máximo 200 caracteres.");
        if (request.Fim <= request.Inicio) throw new InvalidOperationException("O horário de fim deve ser depois do início.");
        if (request.Fim - request.Inicio > TimeSpan.FromHours(12))
            throw new InvalidOperationException("A reunião pode durar no máximo 12 horas.");
        var pauta = (request.Pauta ?? string.Empty).Trim();
        if (pauta.Length > 4000) throw new InvalidOperationException("A pauta deve ter no máximo 4.000 caracteres.");

        RecorrenciaReuniao? recorrencia = null;
        if (request.Repetir is { } repetir)
        {
            if (!Enum.IsDefined(repetir)) throw new InvalidOperationException("Opção de repetição inválida.");
            var primeiroDia = DateOnly.FromDateTime(request.Inicio);
            var ate = request.RepetirAte ?? throw new InvalidOperationException("Informe até quando a reunião se repete.");
            if (ate <= primeiroDia) throw new InvalidOperationException("A data final da repetição deve ser depois do primeiro dia.");
            if (ate > primeiroDia.AddYears(1)) throw new InvalidOperationException("A repetição pode ir no máximo até 1 ano.");
            if (request.Inicio.Date != request.Fim.Date)
                throw new InvalidOperationException("Reunião que se repete deve começar e terminar no mesmo dia.");
            recorrencia = new RecorrenciaReuniao(repetir, ate);
        }

        if (string.IsNullOrWhiteSpace(request.AzureAdObjectId))
            throw new InvalidOperationException("Não foi possível identificar sua conta Microsoft. Entre novamente pelo Teams.");
        var organizador = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct)
            ?? throw new InvalidOperationException("Seu usuário não está vinculado a uma conta Microsoft. Entre novamente pelo Teams.");

        if (_reuniaoTeams is null || !_reuniaoTeams.Configurado)
            throw new ServicoIndisponivelException("A integração com o Teams não está configurada neste ambiente. Marque a reunião pelo Teams.");

        var externos = new List<string>();
        foreach (var bruto in request.EmailsExternos ?? [])
        {
            var email = (bruto ?? string.Empty).Trim();
            if (email.Length == 0) continue;
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var endereco) || endereco.Address != email)
                throw new InvalidOperationException($"E-mail inválido: {email}");
            externos.Add(email);
        }

        var ids = (request.ConvidadosUsuarioIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        var emailsUsuarios = ids.Count == 0
            ? new List<string>()
            : await _db.Usuarios.AsNoTracking()
                .Where(u => ids.Contains(u.Id) && u.Status == StatusUsuario.Ativo && u.Email != "")
                .Select(u => u.Email)
                .ToListAsync(ct);

        var convidados = emailsUsuarios.Concat(externos)
            .Where(e => !string.Equals(e, organizador.Email, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (convidados.Count > MaximoConvidados)
            throw new InvalidOperationException($"Convide no máximo {MaximoConvidados} pessoas por reunião.");

        var descricao = pauta;
        if (request.ObraId is { } obraId)
        {
            var obra = await _db.Obras.AsNoTracking().Where(o => o.Id == obraId).Select(o => o.Nome).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Obra não encontrada.");
            descricao = $"Obra: {obra}" + (pauta.Length > 0 ? $"\n\n{pauta}" : string.Empty);
        }
        descricao += (descricao.Length > 0 ? "\n\n" : string.Empty) + $"Reunião criada pelo sistema SST por {organizador.Nome}.";

        try
        {
            var criada = await _reuniaoTeams.CriarReuniaoAsync(
                organizador.Id, titulo, descricao, request.Inicio, request.Fim, convidados, ct, recorrencia);
            return new ReuniaoCalendarioCriadaDto(criada.GraphEventId, criada.LinkTeams, convidados.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ServicoIndisponivelException(
                "O Teams não aceitou a reunião agora. Tente de novo em instantes; se persistir, marque-a pelo Teams.", ex);
        }
    }
}
