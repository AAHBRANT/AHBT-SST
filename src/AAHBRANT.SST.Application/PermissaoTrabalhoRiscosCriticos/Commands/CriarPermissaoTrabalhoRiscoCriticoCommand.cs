using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRiscosCriticos.Commands;

// §6 do formulário, "Riscos críticos e controles complementares" — tabela livre.
public record CriarPermissaoTrabalhoRiscoCriticoCommand(
    Guid PermissaoTrabalhoId,
    string RiscoCondicao,
    string? ControleComplementar,
    string? ResponsavelEvidencia) : IRequest<Guid>;

public class CriarPermissaoTrabalhoRiscoCriticoCommandValidator : AbstractValidator<CriarPermissaoTrabalhoRiscoCriticoCommand>
{
    public CriarPermissaoTrabalhoRiscoCriticoCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.RiscoCondicao).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ControleComplementar).MaximumLength(500);
        RuleFor(x => x.ResponsavelEvidencia).MaximumLength(200);
    }
}

public class CriarPermissaoTrabalhoRiscoCriticoCommandHandler : IRequestHandler<CriarPermissaoTrabalhoRiscoCriticoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPermissaoTrabalhoRiscoCriticoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPermissaoTrabalhoRiscoCriticoCommand request, CancellationToken ct)
    {
        var ptExiste = await _db.PermissoesTrabalho.AnyAsync(p => p.Id == request.PermissaoTrabalhoId, ct);
        if (!ptExiste)
            throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var risco = new PermissaoTrabalhoRiscoCritico
        {
            PermissaoTrabalhoId = request.PermissaoTrabalhoId,
            RiscoCondicao = request.RiscoCondicao,
            ControleComplementar = request.ControleComplementar,
            ResponsavelEvidencia = request.ResponsavelEvidencia,
        };

        _db.PermissaoTrabalhoRiscosCriticos.Add(risco);
        await _db.SaveChangesAsync(ct);
        return risco.Id;
    }
}
