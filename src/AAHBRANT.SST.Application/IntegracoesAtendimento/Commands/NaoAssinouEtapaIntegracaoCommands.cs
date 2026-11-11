using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Q9 (decisão do usuário): quem não assina a etapa é marcado "Não assinou" com motivo. Sai da lista de presença da turma (sem
// certificado, fora da ata) e não trava o "Avançar". Só na etapa de curso em andamento; dá para desfazer antes de avançar.
// A pessoa não recebe "Integração realizada" enquanto faltar curso exigido (ver RegistroIntegracaoSincronizador).
public record MarcarNaoAssinouEtapaIntegracaoCommand(
    Guid IntegracaoId,
    Guid EtapaId,
    Guid TrabalhadorId,
    string Motivo,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public class MarcarNaoAssinouEtapaIntegracaoCommandValidator : AbstractValidator<MarcarNaoAssinouEtapaIntegracaoCommand>
{
    public MarcarNaoAssinouEtapaIntegracaoCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.EtapaId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().Must(m => m is not null && m.Trim().Length >= 5)
            .WithMessage("Informe o motivo (ao menos 5 caracteres).");
    }
}

public class MarcarNaoAssinouEtapaIntegracaoCommandHandler : IRequestHandler<MarcarNaoAssinouEtapaIntegracaoCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService? _auditoria;

    public MarcarNaoAssinouEtapaIntegracaoCommandHandler(IAppDbContext db, IAuditoriaService? auditoria = null)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(MarcarNaoAssinouEtapaIntegracaoCommand request, CancellationToken ct)
    {
        var (at, responsavel) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 5) throw new InvalidOperationException("Informe o motivo (ao menos 5 caracteres).");
        if (motivo.Length > 300) motivo = motivo[..300];

        var etapa = await RegistrarPeriodoEtapaIntegracaoCommandHandler.EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);
        var sessaoId = etapa.SessaoTreinamentoId
            ?? throw new InvalidOperationException("A turma desta etapa não foi encontrada.");
        if (await _db.NaoAssinouEtapasIntegracao.AnyAsync(n => n.IntegracaoEtapaId == etapa.Id && n.TrabalhadorId == request.TrabalhadorId, ct))
            throw new InvalidOperationException("Esta pessoa já está marcada como \"Não assinou\" nesta etapa.");

        var naTurma = await _db.ParticipantesSessaoTreinamento
            .FirstOrDefaultAsync(p => p.SessaoTreinamentoId == sessaoId && p.TrabalhadorId == request.TrabalhadorId && p.Ativo, ct)
            ?? throw new KeyNotFoundException("Esta pessoa não está na lista desta etapa.");
        if (naTurma.PresencaConfirmadaEm is not null)
            throw new InvalidOperationException("Esta pessoa já assinou a etapa e não pode ser marcada como \"Não assinou\".");

        _db.ParticipantesSessaoTreinamento.Remove(naTurma);
        _db.NaoAssinouEtapasIntegracao.Add(new NaoAssinouEtapaIntegracao
        {
            ObraId = at.ObraId, IntegracaoEtapaId = etapa.Id, TrabalhadorId = request.TrabalhadorId,
            Motivo = motivo, MarcadoEmUtc = DateTime.UtcNow, MarcadoPorUsuarioId = responsavel.UsuarioId,
        });
        at.Versao++;
        if (_auditoria is not null)
            await _auditoria.RegistrarAsync("IntegracaoEtapa.NaoAssinou", nameof(IntegracaoEtapa), etapa.Id, responsavel.UsuarioId,
                request.TrabalhadorId, new { at.Numero, etapa.Ordem, Motivo = motivo }, ct);
        await _db.SaveChangesAsync(ct);
        return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}

public record DesfazerNaoAssinouEtapaIntegracaoCommand(
    Guid IntegracaoId,
    Guid EtapaId,
    Guid TrabalhadorId,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public class DesfazerNaoAssinouEtapaIntegracaoCommandHandler : IRequestHandler<DesfazerNaoAssinouEtapaIntegracaoCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService? _auditoria;

    public DesfazerNaoAssinouEtapaIntegracaoCommandHandler(IAppDbContext db, IAuditoriaService? auditoria = null)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(DesfazerNaoAssinouEtapaIntegracaoCommand request, CancellationToken ct)
    {
        var (at, responsavel) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var etapa = await RegistrarPeriodoEtapaIntegracaoCommandHandler.EtapaDeCursoEmAndamentoAsync(_db, at, request.EtapaId, ct);
        var marca = await _db.NaoAssinouEtapasIntegracao
            .FirstOrDefaultAsync(n => n.IntegracaoEtapaId == etapa.Id && n.TrabalhadorId == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Esta pessoa não está marcada como \"Não assinou\" nesta etapa.");
        var ativo = await _db.Trabalhadores.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.TrabalhadorId && t.Ativo && t.Situacao == AAHBRANT.SST.Domain.Enums.SituacaoTrabalhador.Ativo, ct);
        if (!ativo)
            throw new InvalidOperationException("Esta pessoa está desligada e não volta para a lista da etapa.");

        _db.NaoAssinouEtapasIntegracao.Remove(marca);
        _db.ParticipantesSessaoTreinamento.Add(new ParticipanteSessaoTreinamento
            { SessaoTreinamentoId = etapa.SessaoTreinamentoId!.Value, TrabalhadorId = request.TrabalhadorId });
        at.Versao++;
        if (_auditoria is not null)
            await _auditoria.RegistrarAsync("IntegracaoEtapa.NaoAssinouDesfeito", nameof(IntegracaoEtapa), etapa.Id, responsavel.UsuarioId,
                request.TrabalhadorId, new { at.Numero, etapa.Ordem }, ct);
        await _db.SaveChangesAsync(ct);
        return await MontadorIntegracaoAtendimento.MontarAsync(_db, at.Id, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}
