using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record EncerrarReuniaoCipaCommand(Guid ReuniaoId, string Deliberacoes) : IRequest;

public class EncerrarReuniaoCipaCommandValidator : AbstractValidator<EncerrarReuniaoCipaCommand>
{
    public EncerrarReuniaoCipaCommandValidator()
    {
        RuleFor(x => x.ReuniaoId).NotEmpty();
        RuleFor(x => x.Deliberacoes).NotEmpty().MaximumLength(4000);
    }
}

public class EncerrarReuniaoCipaCommandHandler : IRequestHandler<EncerrarReuniaoCipaCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarReuniaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarReuniaoCipaCommand request, CancellationToken ct)
    {
        var reuniao = await _db.ReunioesCipa.FirstOrDefaultAsync(r => r.Id == request.ReuniaoId, ct)
            ?? throw new KeyNotFoundException($"Reunião {request.ReuniaoId} não encontrada.");

        reuniao.Deliberacoes = request.Deliberacoes;
        reuniao.Status = StatusReuniaoCipa.AtaRegistrada;

        await _db.SaveChangesAsync(ct);
    }
}
