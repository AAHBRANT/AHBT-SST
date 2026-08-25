using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ativos.Commands;

public record ExcluirAtivoSstCommand(Guid Id) : IRequest;

public class ExcluirAtivoSstCommandValidator : AbstractValidator<ExcluirAtivoSstCommand>
{
    public ExcluirAtivoSstCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAtivoSstCommandHandler : IRequestHandler<ExcluirAtivoSstCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAtivoSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAtivoSstCommand request, CancellationToken ct)
    {
        var ativo = await _db.AtivosSst.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Ativo {request.Id} não encontrado.");

        _db.AtivosSst.Remove(ativo);
        await _db.SaveChangesAsync(ct);
    }
}
