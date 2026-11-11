using System.Globalization;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Carga horária da etapa: um ou mais períodos (inclusive em dias diferentes). Só na etapa de curso em andamento.
// Data "yyyy-MM-dd", Inicio/Fim "HH:mm" (texto, para o contrato não depender do formato de DateOnly/TimeOnly).
public record RegistrarPeriodoEtapaIntegracaoCommand(
    Guid IntegracaoId,
    Guid EtapaId,
    string Data,
    string Inicio,
    string Fim,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<Guid>;

public class RegistrarPeriodoEtapaIntegracaoCommandValidator : AbstractValidator<RegistrarPeriodoEtapaIntegracaoCommand>
{
    public RegistrarPeriodoEtapaIntegracaoCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.EtapaId).NotEmpty();
        RuleFor(x => x.Data).NotEmpty();
        RuleFor(x => x.Inicio).NotEmpty();
        RuleFor(x => x.Fim).NotEmpty();
    }
}

public class RegistrarPeriodoEtapaIntegracaoCommandHandler : IRequestHandler<RegistrarPeriodoEtapaIntegracaoCommand, Guid>
{
    private const int MinutosMaximosPorPeriodo = 12 * 60;
    private readonly IAppDbContext _db;

    public RegistrarPeriodoEtapaIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegistrarPeriodoEtapaIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);

        if (!DateTime.TryParseExact(request.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            throw new InvalidOperationException("Informe a data do período (aaaa-mm-dd).");
        if (!TimeSpan.TryParseExact(request.Inicio, @"hh\:mm", CultureInfo.InvariantCulture, out var inicio)
            || !TimeSpan.TryParseExact(request.Fim, @"hh\:mm", CultureInfo.InvariantCulture, out var fim))
            throw new InvalidOperationException("Informe o horário de início e de fim (hh:mm).");
        if (fim <= inicio)
            throw new InvalidOperationException("O horário de fim precisa ser depois do início.");
        var minutos = (int)(fim - inicio).TotalMinutes;
        if (minutos > MinutosMaximosPorPeriodo)
            throw new InvalidOperationException("Um período não pode passar de 12 horas. Divida em períodos menores.");
        if (data.Date > ExigenciasEtapasIntegracao.HojeBrasilia())
            throw new InvalidOperationException("A data do período não pode ser futura.");

        // O período é da própria etapa: não começa antes dela, não está no futuro e a mesma hora não vale para dois cursos.
        var diaDeInicio = AAHBRANT.SST.Application.PlanosIntegracao.AgendaTreinamentosIntegracao.DataBrasilia(etapa.IniciadaEmUtc ?? DateTime.UtcNow);
        if (data.Date < diaDeInicio)
            throw new InvalidOperationException("A data do período não pode ser anterior ao início desta etapa.");
        var agora = AAHBRANT.SST.Application.PlanosIntegracao.AgendaTreinamentosIntegracao.AgoraBrasilia();
        if (data.Date == agora.Date && fim > agora.TimeOfDay)
            throw new InvalidOperationException("O horário de fim não pode estar no futuro. Lance o período depois que ele terminar.");

        var existentes = await _db.PeriodosEtapaIntegracao
            .Where(p => p.IntegracaoEtapaId == etapa.Id && p.Data == data.Date).ToListAsync(ct);
        if (existentes.Any(p => inicio < p.Fim && fim > p.Inicio))
            throw new InvalidOperationException("Este período se sobrepõe a outro já lançado no mesmo dia.");
        var idsEtapas = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id && e.Id != etapa.Id).Select(e => e.Id).ToListAsync(ct);
        var deOutrasEtapas = await _db.PeriodosEtapaIntegracao
            .Where(p => idsEtapas.Contains(p.IntegracaoEtapaId) && p.Data == data.Date).ToListAsync(ct);
        if (deOutrasEtapas.Any(p => inicio < p.Fim && fim > p.Inicio))
            throw new InvalidOperationException("Este horário já foi lançado em outra etapa desta integração. A mesma hora não conta para dois cursos.");

        var periodo = new PeriodoEtapaIntegracao
        {
            ObraId = at.ObraId,
            IntegracaoEtapaId = etapa.Id,
            Data = data.Date,
            Inicio = inicio,
            Fim = fim,
            Minutos = minutos,
        };
        _db.PeriodosEtapaIntegracao.Add(periodo);
        await _db.SaveChangesAsync(ct);
        return periodo.Id;
    }

    internal static async Task<IntegracaoEtapa> EtapaDeCursoEmAndamentoAsync(IAppDbContext db, IntegracaoAtendimento at, Guid etapaId, CancellationToken ct)
    {
        var etapa = await db.IntegracaoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId && e.IntegracaoAtendimentoId == at.Id, ct)
            ?? throw new KeyNotFoundException("Etapa não encontrada nesta integração.");
        if (at.Status != StatusIntegracaoAtendimento.EmAndamento || etapa.Status != StatusEtapaIntegracao.EmAndamento)
            throw new InvalidOperationException("Só dá para lançar a carga horária da etapa em andamento.");
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico)
            throw new InvalidOperationException("A etapa da Ordem de Serviço não tem carga horária.");
        return etapa;
    }
}

public record RemoverPeriodoEtapaIntegracaoCommand(
    Guid IntegracaoId,
    Guid EtapaId,
    Guid PeriodoId,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest;

public class RemoverPeriodoEtapaIntegracaoCommandHandler : IRequestHandler<RemoverPeriodoEtapaIntegracaoCommand>
{
    private readonly IAppDbContext _db;

    public RemoverPeriodoEtapaIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverPeriodoEtapaIntegracaoCommand request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await RegistrarPeriodoEtapaIntegracaoCommandHandler.EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);
        var periodo = await _db.PeriodosEtapaIntegracao.FirstOrDefaultAsync(p => p.Id == request.PeriodoId && p.IntegracaoEtapaId == etapa.Id, ct)
            ?? throw new KeyNotFoundException("Período não encontrado nesta etapa.");
        _db.PeriodosEtapaIntegracao.Remove(periodo);
        await _db.SaveChangesAsync(ct);
    }
}
