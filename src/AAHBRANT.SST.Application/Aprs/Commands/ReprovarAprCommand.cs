using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

public record ReprovarAprCommand(Guid Id, string Motivo) : IRequest;

public class ReprovarAprCommandValidator : AbstractValidator<ReprovarAprCommand>
{
    public ReprovarAprCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}

public class ReprovarAprCommandHandler : IRequestHandler<ReprovarAprCommand>
{
    private readonly IAppDbContext _db;

    public ReprovarAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ReprovarAprCommand request, CancellationToken ct)
    {
        var apr = await _db.Aprs.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"APR {request.Id} não encontrada.");

        apr.Status = StatusApr.Reprovada;
        apr.MotivoReprovacao = request.Motivo;
        apr.AprovadoPorUsuarioId = null;
        apr.DataAprovacao = null;

        await _db.SaveChangesAsync(ct);
    }
}
