using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

public record ExcluirPermissaoTrabalhoCommand(Guid Id) : IRequest;

public class ExcluirPermissaoTrabalhoCommandValidator : AbstractValidator<ExcluirPermissaoTrabalhoCommand>
{
    public ExcluirPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPermissaoTrabalhoCommandHandler : IRequestHandler<ExcluirPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        _db.PermissoesTrabalho.Remove(pt);
        await _db.SaveChangesAsync(ct);
    }
}
