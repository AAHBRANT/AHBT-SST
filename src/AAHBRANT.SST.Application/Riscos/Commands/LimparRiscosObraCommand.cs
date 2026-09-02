using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Commands;

// Companheiro de ImportarRiscosLoteCommand: desfaz uma importação em lote feita por engano ou
// duplicada, removendo todas as avaliações de risco (Riscos) da obra. Não remove Atividade nem
// Perigo (catálogos reaproveitáveis por outras obras/avaliações).
public record LimparRiscosObraCommand(Guid ObraId) : IRequest<int>;

public class LimparRiscosObraCommandValidator : AbstractValidator<LimparRiscosObraCommand>
{
    public LimparRiscosObraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
    }
}

public class LimparRiscosObraCommandHandler : IRequestHandler<LimparRiscosObraCommand, int>
{
    private readonly IAppDbContext _db;

    public LimparRiscosObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(LimparRiscosObraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
        {
            throw new KeyNotFoundException("Obra não encontrada.");
        }

        var riscos = await _db.Riscos
            .Where(r => r.Atividade!.ObraId == request.ObraId)
            .ToListAsync(ct);

        _db.Riscos.RemoveRange(riscos);
        await _db.SaveChangesAsync(ct);

        return riscos.Count;
    }
}
