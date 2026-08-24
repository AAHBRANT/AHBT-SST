using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoControles.Commands;

// "Controles" (§18) — texto livre por controle específico desta PT, mesmo padrão de
// CriarAprEtapaCommand.MedidasPreventivas.
public record CriarPermissaoTrabalhoControleCommand(Guid PermissaoTrabalhoId, string Descricao) : IRequest<Guid>;

public class CriarPermissaoTrabalhoControleCommandValidator : AbstractValidator<CriarPermissaoTrabalhoControleCommand>
{
    public CriarPermissaoTrabalhoControleCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class CriarPermissaoTrabalhoControleCommandHandler : IRequestHandler<CriarPermissaoTrabalhoControleCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPermissaoTrabalhoControleCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPermissaoTrabalhoControleCommand request, CancellationToken ct)
    {
        var ptExiste = await _db.PermissoesTrabalho.AnyAsync(p => p.Id == request.PermissaoTrabalhoId, ct);
        if (!ptExiste)
            throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var controle = new PermissaoTrabalhoControle
        {
            PermissaoTrabalhoId = request.PermissaoTrabalhoId,
            Descricao = request.Descricao
        };

        _db.PermissaoTrabalhoControles.Add(controle);
        await _db.SaveChangesAsync(ct);
        return controle.Id;
    }
}
