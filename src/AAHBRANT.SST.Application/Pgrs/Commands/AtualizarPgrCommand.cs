using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Commands;

public record AtualizarPgrCommand(
    Guid Id,
    Guid ObraId,
    string Nome,
    string? Descricao,
    DateTime DataElaboracao,
    DateTime? DataProximaRevisao,
    DateTime? DataTermino,
    Guid? ResponsavelUsuarioId,
    StatusPgr Status) : IRequest;

public class AtualizarPgrCommandValidator : AbstractValidator<AtualizarPgrCommand>
{
    public AtualizarPgrCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarPgrCommandHandler : IRequestHandler<AtualizarPgrCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPgrCommand request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"PGR {request.Id} não encontrado.");

        pgr.ObraId = request.ObraId;
        pgr.Nome = request.Nome;
        pgr.Descricao = request.Descricao;
        pgr.DataElaboracao = request.DataElaboracao;
        pgr.DataProximaRevisao = request.DataProximaRevisao;
        pgr.DataTermino = request.DataTermino;
        pgr.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        pgr.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
