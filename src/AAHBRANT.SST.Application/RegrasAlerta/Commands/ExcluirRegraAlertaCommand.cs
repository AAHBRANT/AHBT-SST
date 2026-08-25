using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegrasAlerta.Commands;

public record ExcluirRegraAlertaCommand(Guid Id) : IRequest;

public class ExcluirRegraAlertaCommandValidator : AbstractValidator<ExcluirRegraAlertaCommand>
{
    public ExcluirRegraAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirRegraAlertaCommandHandler : IRequestHandler<ExcluirRegraAlertaCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirRegraAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirRegraAlertaCommand request, CancellationToken ct)
    {
        var regra = await _db.RegrasAlerta.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Regra de alerta {request.Id} não encontrada.");

        _db.RegrasAlerta.Remove(regra);
        await _db.SaveChangesAsync(ct);
    }
}
