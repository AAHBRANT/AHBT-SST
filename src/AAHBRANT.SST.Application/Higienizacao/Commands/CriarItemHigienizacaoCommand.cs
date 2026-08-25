using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Higienizacao.Commands;

public record CriarItemHigienizacaoCommand(
    Guid ObraId,
    string Nome,
    string? Local,
    int PeriodicidadeDias) : IRequest<Guid>;

public class CriarItemHigienizacaoCommandValidator : AbstractValidator<CriarItemHigienizacaoCommand>
{
    public CriarItemHigienizacaoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Local).MaximumLength(200);
        RuleFor(x => x.PeriodicidadeDias).GreaterThan(0).WithMessage("A periodicidade deve ser de ao menos 1 dia.");
    }
}

public class CriarItemHigienizacaoCommandHandler : IRequestHandler<CriarItemHigienizacaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarItemHigienizacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarItemHigienizacaoCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var item = new Domain.Entidades.ItemHigienizacao
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            Local = request.Local,
            PeriodicidadeDias = request.PeriodicidadeDias,
        };

        _db.ItensHigienizacao.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
