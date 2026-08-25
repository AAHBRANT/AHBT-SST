using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PcmsoItensMatriz.Commands;

public record ExcluirItemMatrizCommand(Guid Id) : IRequest;

public class ExcluirItemMatrizCommandValidator : AbstractValidator<ExcluirItemMatrizCommand>
{
    public ExcluirItemMatrizCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirItemMatrizCommandHandler : IRequestHandler<ExcluirItemMatrizCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirItemMatrizCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirItemMatrizCommand request, CancellationToken ct)
    {
        var item = await _db.PcmsoItensMatriz.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item de matriz {request.Id} não encontrado.");

        _db.PcmsoItensMatriz.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
