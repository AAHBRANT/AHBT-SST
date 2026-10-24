using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

public record CriarAlojamentoCommand(string Nome, string? Endereco, Guid ObraId) : IRequest<Guid>;

public class CriarAlojamentoCommandValidator : AbstractValidator<CriarAlojamentoCommand>
{
    public CriarAlojamentoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Endereco).MaximumLength(300);
        RuleFor(x => x.ObraId).NotEmpty();
    }
}

public class CriarAlojamentoCommandHandler : IRequestHandler<CriarAlojamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarAlojamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAlojamentoCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var alojamento = new Alojamento
        {
            Nome = request.Nome,
            Endereco = request.Endereco,
            ObraId = request.ObraId,
        };
        _db.Alojamentos.Add(alojamento);
        await _db.SaveChangesAsync(ct);
        return alojamento.Id;
    }
}
