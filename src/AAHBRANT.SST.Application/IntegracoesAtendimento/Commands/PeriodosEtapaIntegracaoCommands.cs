using System.Globalization;
using AAHBRANT.SST.Application.PlanosIntegracao;
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
    internal const string MensagemBloqueio = "A carga horária é contada automaticamente desde a abertura da etapa; não há lançamento manual.";

    private readonly IAppDbContext _db;

    public RegistrarPeriodoEtapaIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegistrarPeriodoEtapaIntegracaoCommand request, CancellationToken ct)
    {
        // 17/11/2026: a carga é contada automaticamente; nenhum lançamento manual. (Regra antiga abaixo fica sem efeito.)
        throw new InvalidOperationException(RegistrarPeriodoEtapaIntegracaoCommandHandler.MensagemBloqueio);
#pragma warning disable CS0162
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);

        var validado = ValidadorPeriodoEtapaIntegracao.Validar(etapa, request.Data, request.Inicio, request.Fim);
        var data = validado.Data;
        var inicio = validado.Inicio;
        var fim = validado.Fim;
        var minutos = validado.Minutos;

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
        // 17/11/2026: a carga é contada automaticamente; nenhum lançamento manual. (Regra antiga abaixo fica sem efeito.)
        throw new InvalidOperationException(RegistrarPeriodoEtapaIntegracaoCommandHandler.MensagemBloqueio);
#pragma warning disable CS0162
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await RegistrarPeriodoEtapaIntegracaoCommandHandler.EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);
        var periodo = await _db.PeriodosEtapaIntegracao.FirstOrDefaultAsync(p => p.Id == request.PeriodoId && p.IntegracaoEtapaId == etapa.Id, ct)
            ?? throw new KeyNotFoundException("Período não encontrado nesta etapa.");
        _db.PeriodosEtapaIntegracao.Remove(periodo);
        await _db.SaveChangesAsync(ct);
    }
}

// Regras de UM período, iguais no lançamento manual e no "ajustar horário" (a sobreposição, que olha o banco ou a lista, fica com quem chama).
internal static class ValidadorPeriodoEtapaIntegracao
{
    private const int MinutosMaximosPorPeriodo = 12 * 60;

    internal sealed record PeriodoValidado(DateTime Data, TimeSpan Inicio, TimeSpan Fim, int Minutos);

    internal static PeriodoValidado Validar(IntegracaoEtapa etapa, string? dataTexto, string? inicioTexto, string? fimTexto)
    {
        if (!DateTime.TryParseExact(dataTexto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            throw new InvalidOperationException("Informe a data do período (aaaa-mm-dd).");
        if (!TimeSpan.TryParseExact(inicioTexto, @"hh\:mm", CultureInfo.InvariantCulture, out var inicio)
            || !TimeSpan.TryParseExact(fimTexto, @"hh\:mm", CultureInfo.InvariantCulture, out var fim))
            throw new InvalidOperationException("Informe o horário de início e de fim (hh:mm).");
        if (fim <= inicio)
            throw new InvalidOperationException("O horário de fim precisa ser depois do início.");
        var minutos = (int)(fim - inicio).TotalMinutes;
        if (minutos > MinutosMaximosPorPeriodo)
            throw new InvalidOperationException("Um período não pode passar de 12 horas. Divida em períodos menores.");
        if (data.Date > ExigenciasEtapasIntegracao.HojeBrasilia())
            throw new InvalidOperationException("A data do período não pode ser futura.");

        // O período é da própria etapa: não começa antes da abertura dela, não está no futuro e a mesma hora não vale para dois cursos.
        var abertura = AgendaTreinamentosIntegracao.HoraBrasilia(etapa.IniciadaEmUtc ?? DateTime.UtcNow);
        var aberturaNoMinuto = new DateTime(abertura.Year, abertura.Month, abertura.Day, abertura.Hour, abertura.Minute, 0);
        if (data.Date < aberturaNoMinuto.Date)
            throw new InvalidOperationException("A data do período não pode ser anterior ao início desta etapa.");
        if (data.Date.Add(inicio) < aberturaNoMinuto)
            throw new InvalidOperationException($"O início do período não pode ser anterior à abertura da etapa ({aberturaNoMinuto:HH:mm}).");
        var agora = AgendaTreinamentosIntegracao.AgoraBrasilia();
        if (data.Date == agora.Date && fim > agora.TimeOfDay)
            throw new InvalidOperationException("O horário de fim não pode estar no futuro. Lance o período depois que ele terminar.");

        return new PeriodoValidado(data.Date, inicio, fim, minutos);
    }
}

public record PeriodoAjusteEtapaIntegracao(string? Data, string? Inicio, string? Fim);

// "Ajustar horário": troca TODOS os períodos da etapa pelos informados, de uma vez (um único SaveChanges: se algo falha, nada muda).
// Os períodos gravados deixam de ser automáticos. A lista permite janelas que cruzam a meia-noite.
public record AjustarPeriodosEtapaIntegracaoCommand(
    Guid IntegracaoId,
    Guid EtapaId,
    IReadOnlyList<PeriodoAjusteEtapaIntegracao> Periodos,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest;

public class AjustarPeriodosEtapaIntegracaoCommandValidator : AbstractValidator<AjustarPeriodosEtapaIntegracaoCommand>
{
    public AjustarPeriodosEtapaIntegracaoCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.EtapaId).NotEmpty();
        RuleFor(x => x.Periodos).NotEmpty().WithMessage("Informe pelo menos um período.");
        RuleFor(x => x.Periodos.Count).LessThanOrEqualTo(31).WithMessage("Informe no máximo 31 períodos.");
    }
}

public class AjustarPeriodosEtapaIntegracaoCommandHandler : IRequestHandler<AjustarPeriodosEtapaIntegracaoCommand>
{
    private readonly IAppDbContext _db;

    public AjustarPeriodosEtapaIntegracaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AjustarPeriodosEtapaIntegracaoCommand request, CancellationToken ct)
    {
        // 17/11/2026: a carga é contada automaticamente; nenhum lançamento manual. (Regra antiga abaixo fica sem efeito.)
        throw new InvalidOperationException(RegistrarPeriodoEtapaIntegracaoCommandHandler.MensagemBloqueio);
#pragma warning disable CS0162
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await RegistrarPeriodoEtapaIntegracaoCommandHandler.EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);
        if (request.Periodos is null || request.Periodos.Count == 0)
            throw new InvalidOperationException("Informe pelo menos um período.");

        var novos = request.Periodos
            .Select(p => ValidadorPeriodoEtapaIntegracao.Validar(etapa, p.Data, p.Inicio, p.Fim))
            .OrderBy(p => p.Data).ThenBy(p => p.Inicio)
            .ToList();

        for (var i = 1; i < novos.Count; i++)
            if (novos[i].Data == novos[i - 1].Data && novos[i].Inicio < novos[i - 1].Fim)
                throw new InvalidOperationException("Dois dos períodos informados se sobrepõem no mesmo dia.");

        var idsOutras = await _db.IntegracaoEtapas.Where(e => e.IntegracaoAtendimentoId == at.Id && e.Id != etapa.Id).Select(e => e.Id).ToListAsync(ct);
        var datas = novos.Select(p => p.Data).Distinct().ToList();
        var deOutrasEtapas = await _db.PeriodosEtapaIntegracao
            .Where(p => idsOutras.Contains(p.IntegracaoEtapaId) && datas.Contains(p.Data)).ToListAsync(ct);
        if (novos.Any(n => deOutrasEtapas.Any(o => o.Data == n.Data && n.Inicio < o.Fim && n.Fim > o.Inicio)))
            throw new InvalidOperationException("Este horário já foi lançado em outra etapa desta integração. A mesma hora não conta para dois cursos.");

        var atuais = await _db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapa.Id).ToListAsync(ct);
        _db.PeriodosEtapaIntegracao.RemoveRange(atuais);
        foreach (var n in novos)
        {
            // Janela que cruza a meia-noite: o período que termina às 23:59 e é seguido por outro à 00:00 do dia seguinte conta o minuto
            // até a meia-noite (igual ao período automático), para a soma bater com o tempo real.
            var minutos = n.Minutos;
            if (n.Fim == PeriodosAutomaticosEtapa.FimDoDia
                && novos.Any(x => x.Data == n.Data.AddDays(1) && x.Inicio == TimeSpan.Zero))
                minutos++;
            _db.PeriodosEtapaIntegracao.Add(new PeriodoEtapaIntegracao
            {
                ObraId = at.ObraId,
                IntegracaoEtapaId = etapa.Id,
                Data = n.Data,
                Inicio = n.Inicio,
                Fim = n.Fim,
                Minutos = minutos,
                Automatico = false,
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
