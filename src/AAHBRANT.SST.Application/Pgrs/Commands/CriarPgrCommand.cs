using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Pgrs.Commands;

public record CriarPgrCommand(
    Guid ObraId,
    string Nome,
    string? Descricao,
    DateTime DataElaboracao,
    DateTime? DataProximaRevisao,
    Guid? ResponsavelUsuarioId,
    StatusPgr Status) : IRequest<Guid>;

public class CriarPgrCommandValidator : AbstractValidator<CriarPgrCommand>
{
    public CriarPgrCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class CriarPgrCommandHandler : IRequestHandler<CriarPgrCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPgrCommand request, CancellationToken ct)
    {
        var pgr = new Pgr
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            Descricao = request.Descricao,
            DataElaboracao = request.DataElaboracao,
            DataProximaRevisao = request.DataProximaRevisao,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Status = request.Status
        };

        _db.Pgrs.Add(pgr);
        await _db.SaveChangesAsync(ct);
        return pgr.Id;
    }
}
