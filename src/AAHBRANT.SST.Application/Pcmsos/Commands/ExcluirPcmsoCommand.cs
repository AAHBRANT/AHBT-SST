using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record ExcluirPcmsoCommand(Guid Id) : IRequest;

public class ExcluirPcmsoCommandValidator : AbstractValidator<ExcluirPcmsoCommand>
{
    public ExcluirPcmsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPcmsoCommandHandler : IRequestHandler<ExcluirPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPcmsoCommand request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.Id} não encontrado.");

        _db.PcmsoDetalhes.Remove(pcmso);
        await _db.SaveChangesAsync(ct);
    }
}
