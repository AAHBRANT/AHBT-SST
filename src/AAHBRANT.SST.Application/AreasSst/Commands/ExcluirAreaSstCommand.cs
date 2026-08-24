using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AreasSst.Commands;

public record ExcluirAreaSstCommand(Guid Id) : IRequest;

public class ExcluirAreaSstCommandValidator : AbstractValidator<ExcluirAreaSstCommand>
{
    public ExcluirAreaSstCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirAreaSstCommandHandler : IRequestHandler<ExcluirAreaSstCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirAreaSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirAreaSstCommand request, CancellationToken ct)
    {
        var area = await _db.AreasSst.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Área {request.Id} não encontrada.");

        _db.AreasSst.Remove(area);
        await _db.SaveChangesAsync(ct);
    }
}
