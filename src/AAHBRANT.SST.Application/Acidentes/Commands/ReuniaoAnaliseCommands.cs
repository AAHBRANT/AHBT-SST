using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

// Reunião de análise pela tela da ocorrência (23/10/2026, desenho aprovado em
// docs/mockups/reuniao-analise-ocorrencia-v1.html): agendar, remarcar, tentar de novo no Teams e
// registrar que aconteceu (presentes + ata). Obrigatória em toda ocorrência — a finalização da
// investigação exige a reunião realizada (AvancarStatusAcidenteCommand).

public record AgendarReuniaoAnaliseCommand(
    Guid AcidenteId, DateTime Inicio, int DuracaoMinutos, List<Guid> ParticipantesUsuarioIds, Guid? UsuarioAtualId = null) : IRequest;

public record RemarcarReuniaoAnaliseCommand(Guid AcidenteId, DateTime Inicio, int DuracaoMinutos) : IRequest;

public record TentarReuniaoAnaliseNoTeamsCommand(Guid AcidenteId) : IRequest;

public record RegistrarReuniaoAnaliseRealizadaCommand(Guid AcidenteId, DateTime RealizadaEm, List<string> Presentes, string Ata) : IRequest;

public class AgendarReuniaoAnaliseCommandValidator : AbstractValidator<AgendarReuniaoAnaliseCommand>
{
    public AgendarReuniaoAnaliseCommandValidator()
    {
        RuleFor(x => x.AcidenteId).NotEmpty();
        RuleFor(x => x.Inicio).NotEmpty().WithMessage("Informe a data e o horário da reunião.");
        RuleFor(x => x.DuracaoMinutos).InclusiveBetween(15, 240);
    }
}

public class RemarcarReuniaoAnaliseCommandValidator : AbstractValidator<RemarcarReuniaoAnaliseCommand>
{
    public RemarcarReuniaoAnaliseCommandValidator()
    {
        RuleFor(x => x.AcidenteId).NotEmpty();
        RuleFor(x => x.Inicio).NotEmpty().WithMessage("Informe a nova data e o novo horário da reunião.");
        RuleFor(x => x.DuracaoMinutos).InclusiveBetween(15, 240);
    }
}

public class RegistrarReuniaoAnaliseRealizadaCommandValidator : AbstractValidator<RegistrarReuniaoAnaliseRealizadaCommand>
{
    public RegistrarReuniaoAnaliseRealizadaCommandValidator()
    {
        RuleFor(x => x.AcidenteId).NotEmpty();
        RuleFor(x => x.RealizadaEm).NotEmpty().WithMessage("Informe quando a reunião aconteceu.");
        RuleFor(x => x.Presentes)
            .Must(p => p is not null && p.Any(n => !string.IsNullOrWhiteSpace(n)))
            .WithMessage("Marque quem participou da reunião.");
        RuleFor(x => x.Ata).NotEmpty().WithMessage("Escreva a ata: o que foi discutido e decidido.").MaximumLength(4000);
    }
}

internal static class ReuniaoAnaliseCarga
{
    public static async Task<Acidente> AcidenteEditavelAsync(IAppDbContext db, Guid acidenteId, CancellationToken ct)
    {
        var acidente = await db.Acidentes.FirstOrDefaultAsync(a => a.Id == acidenteId, ct)
            ?? throw new KeyNotFoundException($"Ocorrência {acidenteId} não encontrada.");
        if (acidente.Status == StatusAcidente.Concluido)
            throw new InvalidOperationException("A investigação desta ocorrência já foi concluída; a reunião não pode mais ser alterada.");
        return acidente;
    }

    public static async Task<ReuniaoAnaliseOcorrencia> ReuniaoAsync(IAppDbContext db, Guid acidenteId, CancellationToken ct)
        => await db.ReunioesAnaliseOcorrencia
               .Where(r => r.AcidenteId == acidenteId)
               .OrderByDescending(r => r.CreatedAtUtc)
               .FirstOrDefaultAsync(ct)
           ?? throw new InvalidOperationException("Esta ocorrência ainda não tem reunião de análise. Agende a reunião primeiro.");
}

public class AgendarReuniaoAnaliseCommandHandler : IRequestHandler<AgendarReuniaoAnaliseCommand>
{
    private readonly IAppDbContext _db;
    private readonly IReuniaoTeamsService? _reuniaoTeams;

    public AgendarReuniaoAnaliseCommandHandler(IAppDbContext db, IReuniaoTeamsService? reuniaoTeams = null)
    {
        _db = db;
        _reuniaoTeams = reuniaoTeams;
    }

    public async Task Handle(AgendarReuniaoAnaliseCommand request, CancellationToken ct)
    {
        var acidente = await ReuniaoAnaliseCarga.AcidenteEditavelAsync(_db, request.AcidenteId, ct);
        if (await _db.ReunioesAnaliseOcorrencia.AnyAsync(r => r.AcidenteId == acidente.Id, ct))
            throw new InvalidOperationException("Esta ocorrência já tem reunião de análise. Use Remarcar para mudar a data.");

        var (reuniao, emails) = await ReuniaoAnaliseOcorrenciaServico.RegistrarAsync(
            _db, acidente, request.Inicio, request.DuracaoMinutos, request.ParticipantesUsuarioIds, request.UsuarioAtualId, ct);
        await _db.SaveChangesAsync(ct);
        await ReuniaoAnaliseOcorrenciaServico.MarcarNoTeamsAsync(_db, _reuniaoTeams, acidente, reuniao, emails, ct);
    }
}

public class RemarcarReuniaoAnaliseCommandHandler : IRequestHandler<RemarcarReuniaoAnaliseCommand>
{
    private readonly IAppDbContext _db;
    private readonly IReuniaoTeamsService? _reuniaoTeams;

    public RemarcarReuniaoAnaliseCommandHandler(IAppDbContext db, IReuniaoTeamsService? reuniaoTeams = null)
    {
        _db = db;
        _reuniaoTeams = reuniaoTeams;
    }

    public async Task Handle(RemarcarReuniaoAnaliseCommand request, CancellationToken ct)
    {
        var acidente = await ReuniaoAnaliseCarga.AcidenteEditavelAsync(_db, request.AcidenteId, ct);
        var reuniao = await ReuniaoAnaliseCarga.ReuniaoAsync(_db, acidente.Id, ct);
        if (reuniao.RealizadaEm is not null)
            throw new InvalidOperationException("A reunião já foi registrada como realizada; não dá para remarcar.");

        reuniao.Inicio = request.Inicio;
        reuniao.Fim = request.Inicio.AddMinutes(request.DuracaoMinutos);
        var acao = reuniao.AcaoPlanoId is { } acaoId ? await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == acaoId, ct) : null;
        if (acao is not null)
            acao.Prazo = request.Inicio;
        await _db.SaveChangesAsync(ct);

        // Nunca chegou ao Teams: tenta criar agora, já no horário novo.
        if (reuniao.Situacao != SituacaoReuniaoTeams.Criada || reuniao.GraphEventId is null || reuniao.OrganizadorUsuarioId is null)
        {
            var emails = await ReuniaoAnaliseOcorrenciaServico.EmailsConvidadosAsync(_db, reuniao, ct);
            await ReuniaoAnaliseOcorrenciaServico.MarcarNoTeamsAsync(_db, _reuniaoTeams, acidente, reuniao, emails, ct);
            return;
        }

        // Já está no Teams: muda o horário lá. Falha não desfaz o horário novo gravado aqui.
        reuniao.MotivoFalha = null;
        if (_reuniaoTeams is null || !_reuniaoTeams.Configurado)
            reuniao.MotivoFalha = "Horário mudado aqui, mas a integração com o Teams não está configurada. Ajuste o convite no Teams manualmente.";
        else
        {
            try
            {
                await _reuniaoTeams.RemarcarReuniaoAsync(reuniao.OrganizadorUsuarioId.Value, reuniao.GraphEventId, reuniao.Inicio, reuniao.Fim, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                reuniao.MotivoFalha = ReuniaoAnaliseOcorrenciaServico.Limitar(
                    $"Horário mudado aqui, mas o Teams não aceitou a mudança ({ex.Message}). Ajuste o convite no Teams manualmente.", 500);
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class TentarReuniaoAnaliseNoTeamsCommandHandler : IRequestHandler<TentarReuniaoAnaliseNoTeamsCommand>
{
    private readonly IAppDbContext _db;
    private readonly IReuniaoTeamsService? _reuniaoTeams;

    public TentarReuniaoAnaliseNoTeamsCommandHandler(IAppDbContext db, IReuniaoTeamsService? reuniaoTeams = null)
    {
        _db = db;
        _reuniaoTeams = reuniaoTeams;
    }

    public async Task Handle(TentarReuniaoAnaliseNoTeamsCommand request, CancellationToken ct)
    {
        var acidente = await ReuniaoAnaliseCarga.AcidenteEditavelAsync(_db, request.AcidenteId, ct);
        var reuniao = await ReuniaoAnaliseCarga.ReuniaoAsync(_db, acidente.Id, ct);
        if (reuniao.Situacao == SituacaoReuniaoTeams.Criada)
            throw new InvalidOperationException("A reunião já está marcada no Teams.");

        var emails = await ReuniaoAnaliseOcorrenciaServico.EmailsConvidadosAsync(_db, reuniao, ct);
        await ReuniaoAnaliseOcorrenciaServico.MarcarNoTeamsAsync(_db, _reuniaoTeams, acidente, reuniao, emails, ct);
    }
}

public class RegistrarReuniaoAnaliseRealizadaCommandHandler : IRequestHandler<RegistrarReuniaoAnaliseRealizadaCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarReuniaoAnaliseRealizadaCommandHandler(IAppDbContext db) => _db = db;

    // Também serve para editar a ata depois: grava por cima, enquanto a investigação estiver aberta.
    public async Task Handle(RegistrarReuniaoAnaliseRealizadaCommand request, CancellationToken ct)
    {
        var acidente = await ReuniaoAnaliseCarga.AcidenteEditavelAsync(_db, request.AcidenteId, ct);
        var reuniao = await ReuniaoAnaliseCarga.ReuniaoAsync(_db, acidente.Id, ct);

        reuniao.RealizadaEm = request.RealizadaEm;
        reuniao.Presentes = string.Join("\n", request.Presentes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct());
        reuniao.Ata = request.Ata.Trim();

        // A ação "Realizar a reunião de análise" do plano se conclui sozinha.
        var acao = reuniao.AcaoPlanoId is { } acaoId ? await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == acaoId, ct) : null;
        if (acao is not null && acao.Status != StatusControleRisco.Concluido)
        {
            acao.Status = StatusControleRisco.Concluido;
            acao.DataConclusao = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
