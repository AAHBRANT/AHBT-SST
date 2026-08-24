using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

// Escalonamento manual, disparado por quem está usando a tela — o escalonamento AUTOMÁTICO por
// DataLimiteTratamento vencido (recomendação da "Análise de Oportunidades", Nível 2) dependeria do
// mesmo Worker/job agendado ainda inexistente no projeto, citado em CriarAlertaCommand. Este Command
// cobre só o caminho manual: alguém decide escalonar antes/depois do prazo.
public record EscalonarAlertaCommand(Guid Id, Guid EscalonadoParaUsuarioId) : IRequest;

public class EscalonarAlertaCommandValidator : AbstractValidator<EscalonarAlertaCommand>
{
    public EscalonarAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EscalonadoParaUsuarioId).NotEmpty();
    }
}

public class EscalonarAlertaCommandHandler : IRequestHandler<EscalonarAlertaCommand>
{
    private readonly IAppDbContext _db;

    public EscalonarAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EscalonarAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        if (alerta.Status is StatusAlerta.Resolvido or StatusAlerta.Ignorado)
            throw new InvalidOperationException("Não é possível escalonar um alerta já resolvido ou ignorado.");

        if (!await _db.Usuarios.AnyAsync(u => u.Id == request.EscalonadoParaUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.EscalonadoParaUsuarioId} não encontrado.");

        alerta.Status = StatusAlerta.Escalonado;
        alerta.EscalonadoParaUsuarioId = request.EscalonadoParaUsuarioId;
        alerta.DataEscalonamento = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
