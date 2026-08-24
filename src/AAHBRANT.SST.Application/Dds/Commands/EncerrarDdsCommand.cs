using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record EncerrarDdsCommand(Guid Id) : IRequest;

public class EncerrarDdsCommandValidator : AbstractValidator<EncerrarDdsCommand>
{
    public EncerrarDdsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarDdsCommandHandler : IRequestHandler<EncerrarDdsCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarDdsCommand request, CancellationToken ct)
    {
        var dds = await _db.Dds.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        dds.Status = StatusDds.Concluido;
        await _db.SaveChangesAsync(ct);
    }
}
