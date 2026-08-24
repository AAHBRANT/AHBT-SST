using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Commands;

public record ExcluirMatrizRiscoConfigCommand(Guid Id) : IRequest;

public class ExcluirMatrizRiscoConfigCommandValidator : AbstractValidator<ExcluirMatrizRiscoConfigCommand>
{
    public ExcluirMatrizRiscoConfigCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirMatrizRiscoConfigCommandHandler : IRequestHandler<ExcluirMatrizRiscoConfigCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirMatrizRiscoConfigCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirMatrizRiscoConfigCommand request, CancellationToken ct)
    {
        var config = await _db.MatrizRiscoConfigs.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"MatrizRiscoConfig {request.Id} não encontrada.");

        _db.MatrizRiscoConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);
    }
}
