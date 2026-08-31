using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRiscosCriticos.Commands;

public record ExcluirPermissaoTrabalhoRiscoCriticoCommand(Guid Id) : IRequest;

public class ExcluirPermissaoTrabalhoRiscoCriticoCommandValidator : AbstractValidator<ExcluirPermissaoTrabalhoRiscoCriticoCommand>
{
    public ExcluirPermissaoTrabalhoRiscoCriticoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPermissaoTrabalhoRiscoCriticoCommandHandler : IRequestHandler<ExcluirPermissaoTrabalhoRiscoCriticoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPermissaoTrabalhoRiscoCriticoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPermissaoTrabalhoRiscoCriticoCommand request, CancellationToken ct)
    {
        var risco = await _db.PermissaoTrabalhoRiscosCriticos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco crítico {request.Id} não encontrado.");

        _db.PermissaoTrabalhoRiscosCriticos.Remove(risco);
        await _db.SaveChangesAsync(ct);
    }
}
