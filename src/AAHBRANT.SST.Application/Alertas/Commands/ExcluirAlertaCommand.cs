using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alertas.Commands;

public record ExcluirAlertaCommand(Guid Id) : IRequest;

public class ExcluirAlertaCommandValidator : AbstractValidator<ExcluirAlertaCommand>
{
    public ExcluirAlertaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAlertaCommandHandler : IRequestHandler<ExcluirAlertaCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAlertaCommand request, CancellationToken ct)
    {
        var alerta = await _db.Alertas.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Alerta {request.Id} não encontrado.");

        _db.Alertas.Remove(alerta);
        await _db.SaveChangesAsync(ct);
    }
}
