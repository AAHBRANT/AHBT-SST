using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoControles.Commands;

public record ExcluirPermissaoTrabalhoControleCommand(Guid Id) : IRequest;

public class ExcluirPermissaoTrabalhoControleCommandValidator : AbstractValidator<ExcluirPermissaoTrabalhoControleCommand>
{
    public ExcluirPermissaoTrabalhoControleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPermissaoTrabalhoControleCommandHandler : IRequestHandler<ExcluirPermissaoTrabalhoControleCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPermissaoTrabalhoControleCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPermissaoTrabalhoControleCommand request, CancellationToken ct)
    {
        var controle = await _db.PermissaoTrabalhoControles.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Controle {request.Id} não encontrado.");

        _db.PermissaoTrabalhoControles.Remove(controle);
        await _db.SaveChangesAsync(ct);
    }
}
