using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanoAcao.Commands;

public record AtualizarPlanoAcaoItemCommand(
    Guid Id,
    Guid? RiscoId,
    string Descricao,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo,
    DateTime? DataConclusao,
    StatusControleRisco Status) : IRequest;

public class AtualizarPlanoAcaoItemCommandValidator : AbstractValidator<AtualizarPlanoAcaoItemCommand>
{
    public AtualizarPlanoAcaoItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class AtualizarPlanoAcaoItemCommandHandler : IRequestHandler<AtualizarPlanoAcaoItemCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPlanoAcaoItemCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPlanoAcaoItemCommand request, CancellationToken ct)
    {
        var item = await _db.PlanoAcaoItens.NoEscopoDaObra(_db).FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item de plano de ação {request.Id} não encontrado.");

        if (request.RiscoId.HasValue && request.RiscoId != item.RiscoId)
            await _db.GarantirRiscoNoEscopoAsync(request.RiscoId.Value, ct);

        item.RiscoId = request.RiscoId;
        item.Descricao = request.Descricao;
        item.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        item.Prazo = request.Prazo;
        item.DataConclusao = request.DataConclusao;
        item.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
