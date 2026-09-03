using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Dia sem expediente — feriado, folga, obra parada (pedido do usuário, 03/09): em vez de forçar um
// registro de DDS ou deixar o dia mudo, o responsável marca o dia com o motivo. Mesmas validações de
// posição na semana que CriarDdsCommand, mas sem atividades/roteiro/checklist — o dia nasce direto
// com Status=Concluido (ver disclosure em Dds.SemExpediente), então já conta para o encerramento da
// semana sem precisar de fotos de evidência.
public record RegistrarDiaSemExpedienteCommand(Guid DdsSemanalId, DateTime Data, string Motivo) : IRequest<Guid>;

public class RegistrarDiaSemExpedienteCommandValidator : AbstractValidator<RegistrarDiaSemExpedienteCommand>
{
    public RegistrarDiaSemExpedienteCommandValidator()
    {
        RuleFor(x => x.DdsSemanalId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Informe o motivo (feriado, folga, obra parada etc.).").MaximumLength(500);
    }
}

public class RegistrarDiaSemExpedienteCommandHandler : IRequestHandler<RegistrarDiaSemExpedienteCommand, Guid>
{
    private readonly IAppDbContext _db;

    public RegistrarDiaSemExpedienteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegistrarDiaSemExpedienteCommand request, CancellationToken ct)
    {
        var semanal = await _db.DdsSemanais.FirstOrDefaultAsync(s => s.Id == request.DdsSemanalId, ct)
            ?? throw new KeyNotFoundException($"DDS semanal {request.DdsSemanalId} não encontrado.");
        if (semanal.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Esta semana já foi encerrada — não é possível criar novos registros diários.");
        if (request.Data.Date < semanal.DataInicioSemana.Date || request.Data.Date > semanal.DataFimSemana.Date)
            throw new InvalidOperationException("A data do registro precisa estar dentro da semana selecionada (segunda a sexta).");

        var jaExisteNoDia = await _db.Dds.AnyAsync(d => d.DdsSemanalId == semanal.Id && d.Data.Date == request.Data.Date, ct);
        if (jaExisteNoDia)
            throw new InvalidOperationException("Já existe um registro de DDS para este dia da semana.");

        var dds = new Domain.Entidades.Dds
        {
            ObraId = semanal.ObraId,
            DdsSemanalId = semanal.Id,
            Data = request.Data.Date,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
            SemExpediente = true,
            MotivoSemExpediente = request.Motivo,
            Status = StatusDds.Concluido,
        };

        _db.Dds.Add(dds);
        await _db.SaveChangesAsync(ct);
        return dds.Id;
    }
}
