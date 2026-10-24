using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record EncerrarDdsCommand(Guid Id) : IRequest;

public class EncerrarDdsCommandValidator : AbstractValidator<EncerrarDdsCommand>
{
    public EncerrarDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarDdsCommandHandler : IRequestHandler<EncerrarDdsCommand>
{
    private readonly IAppDbContext _db;
    private readonly IMediator? _mediator;
    private readonly ILogger<EncerrarDdsCommandHandler> _logger;

    public EncerrarDdsCommandHandler(IAppDbContext db, IMediator? mediator = null, ILogger<EncerrarDdsCommandHandler>? logger = null)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger ?? NullLogger<EncerrarDdsCommandHandler>.Instance;
    }

    private const int TotalFotosObrigatorias = 3;

    public async Task Handle(EncerrarDdsCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        // Pedido do usuário (31/08): "obrigatoriedade de 3 fotos por registro de DDS para
        // liberação do encerramento" — evidência do registro em si, distinta da foto de presença
        // por participante (que já é obrigatória em RegistrarParticipanteCommand).
        var totalFotos = await _db.DdsFotosEvidencia.CountAsync(f => f.DdsId == dds.Id && f.Ativo, ct);
        if (totalFotos < TotalFotosObrigatorias)
            throw new InvalidOperationException(
                $"São necessárias {TotalFotosObrigatorias} fotos de evidência para encerrar o DDS do dia (faltam {TotalFotosObrigatorias - totalFotos}).");

        var fotos = await _db.DdsFotosEvidencia.Where(f => f.DdsId == dds.Id && f.Ativo).OrderBy(f => f.Ordem).ToListAsync(ct);
        foreach (var foto in fotos)
            AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(foto.FotoMetadadosJson, $"Foto {foto.Ordem}");

        dds.Status = StatusDds.Concluido;
        dds.EncerradoEm = DateTime.UtcNow;

        // DDS realizado com um tema agendado por ocorrência (vinculado na criação do registro do dia):
        // conclui a ação "DDS do dia seguinte" do plano de ação dela.
        var agendamento = await _db.TemasDdsAgendados.FirstOrDefaultAsync(t => t.DdsId == dds.Id, ct);
        if (agendamento is not null)
        {
            agendamento.AplicadoEm = dds.EncerradoEm;
            var acao = agendamento.AcaoPlanoId is null
                ? null
                : await _db.AcoesPlano.FirstOrDefaultAsync(a => a.Id == agendamento.AcaoPlanoId, ct);
            if (acao is not null && acao.Status != StatusControleRisco.Concluido)
            {
                acao.Status = StatusControleRisco.Concluido;
                acao.DataConclusao = dds.EncerradoEm;
            }
        }

        await _db.SaveChangesAsync(ct);

        // DDS encerrado não recebe mais participantes: o documento de assinatura dele é fechado junto.
        if (_mediator is not null)
            await FinalizacaoAutomaticaDocumento.TentarAsync(_db, _mediator, _logger, nameof(Domain.Entidades.Dds), dds.Id, ct);
    }
}
