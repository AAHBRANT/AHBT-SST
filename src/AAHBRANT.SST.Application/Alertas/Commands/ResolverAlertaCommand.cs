using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

public record ResolverAlertaCommand(Guid Id) : IRequest;

public class ResolverAlertaCommandValidator : AbstractValidator<ResolverAlertaCommand>
{
    public ResolverAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ResolverAlertaCommandHandler : IRequestHandler<ResolverAlertaCommand>
{
    private readonly IAppDbContext _db;

    public ResolverAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ResolverAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        if (alerta.Status is StatusAlerta.Resolvido or StatusAlerta.Ignorado)
            throw new InvalidOperationException("Este alerta já está encerrado.");

        alerta.Status = StatusAlerta.Resolvido;
        await _db.SaveChangesAsync(ct);
    }
}
