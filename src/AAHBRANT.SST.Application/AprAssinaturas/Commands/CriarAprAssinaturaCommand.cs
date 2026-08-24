using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.AprAssinaturas.Commands;

// "Assinatura" (§17) — confirmação de ciência por pessoa envolvida, append-only (sem edição/
// exclusão), mesmo padrão de PgrRevisao. Ver disclosure completo em Apr.cs sobre não ser
// assinatura criptográfica/ICP-Brasil.
public record CriarAprAssinaturaCommand(
    Guid AprId,
    Guid TrabalhadorId,
    PapelAssinaturaApr Papel) : IRequest<Guid>;

public class CriarAprAssinaturaCommandValidator : AbstractValidator<CriarAprAssinaturaCommand>
{
    public CriarAprAssinaturaCommandValidator()
    {
        RuleFor(x => x.AprId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Papel).IsInEnum();
    }
}

public class CriarAprAssinaturaCommandHandler : IRequestHandler<CriarAprAssinaturaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAprAssinaturaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAprAssinaturaCommand request, CancellationToken ct)
    {
        var aprExiste = await _db.Aprs.AnyAsync(a => a.Id == request.AprId, ct);
        if (!aprExiste)
            throw new KeyNotFoundException($"APR {request.AprId} não encontrada.");

        var trabalhadorExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct);
        if (!trabalhadorExiste)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        var jaAssinouNessePapel = await _db.AprAssinaturas.AnyAsync(
            s => s.AprId == request.AprId && s.TrabalhadorId == request.TrabalhadorId && s.Papel == request.Papel, ct);
        if (jaAssinouNessePapel)
            throw new InvalidOperationException("Este trabalhador já assinou esta APR com este papel.");

        var assinatura = new AprAssinatura
        {
            AprId = request.AprId,
            TrabalhadorId = request.TrabalhadorId,
            Papel = request.Papel,
            DataAssinatura = DateTime.UtcNow
        };

        _db.AprAssinaturas.Add(assinatura);
        await _db.SaveChangesAsync(ct);
        return assinatura.Id;
    }
}
