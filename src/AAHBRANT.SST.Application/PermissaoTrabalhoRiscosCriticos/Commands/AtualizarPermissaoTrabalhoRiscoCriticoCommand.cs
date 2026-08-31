using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRiscosCriticos.Commands;

public record AtualizarPermissaoTrabalhoRiscoCriticoCommand(
    Guid Id,
    string RiscoCondicao,
    string? ControleComplementar,
    string? ResponsavelEvidencia) : IRequest;

public class AtualizarPermissaoTrabalhoRiscoCriticoCommandValidator : AbstractValidator<AtualizarPermissaoTrabalhoRiscoCriticoCommand>
{
    public AtualizarPermissaoTrabalhoRiscoCriticoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RiscoCondicao).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ControleComplementar).MaximumLength(500);
        RuleFor(x => x.ResponsavelEvidencia).MaximumLength(200);
    }
}

public class AtualizarPermissaoTrabalhoRiscoCriticoCommandHandler : IRequestHandler<AtualizarPermissaoTrabalhoRiscoCriticoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPermissaoTrabalhoRiscoCriticoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPermissaoTrabalhoRiscoCriticoCommand request, CancellationToken ct)
    {
        var risco = await _db.PermissaoTrabalhoRiscosCriticos.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Risco crítico {request.Id} não encontrado.");

        risco.RiscoCondicao = request.RiscoCondicao;
        risco.ControleComplementar = request.ControleComplementar;
        risco.ResponsavelEvidencia = request.ResponsavelEvidencia;

        await _db.SaveChangesAsync(ct);
    }
}
