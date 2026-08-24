using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.PlanoAcao.Commands;

public record CriarPlanoAcaoItemCommand(
    Guid PgrId,
    Guid? RiscoId,
    string Descricao,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo,
    StatusControleRisco Status) : IRequest<Guid>;

public class CriarPlanoAcaoItemCommandValidator : AbstractValidator<CriarPlanoAcaoItemCommand>
{
    public CriarPlanoAcaoItemCommandValidator()
    {
        RuleFor(x => x.PgrId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class CriarPlanoAcaoItemCommandHandler : IRequestHandler<CriarPlanoAcaoItemCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPlanoAcaoItemCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPlanoAcaoItemCommand request, CancellationToken ct)
    {
        var item = new PlanoAcaoItem
        {
            PgrId = request.PgrId,
            RiscoId = request.RiscoId,
            Descricao = request.Descricao,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Prazo = request.Prazo,
            Status = request.Status
        };

        _db.PlanoAcaoItens.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
