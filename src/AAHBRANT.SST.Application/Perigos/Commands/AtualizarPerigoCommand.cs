using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Perigos.Commands;

public record AtualizarPerigoCommand(Guid Id, string Nome, string? Agente, string? Fonte, string? Descricao) : IRequest;

public class AtualizarPerigoCommandValidator : AbstractValidator<AtualizarPerigoCommand>
{
    public AtualizarPerigoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarPerigoCommandHandler : IRequestHandler<AtualizarPerigoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPerigoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPerigoCommand request, CancellationToken ct)
    {
        var perigo = await _db.Perigos.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Perigo {request.Id} não encontrado.");

        perigo.Nome = request.Nome;
        perigo.Agente = request.Agente;
        perigo.Fonte = request.Fonte;
        perigo.Descricao = request.Descricao;

        await _db.SaveChangesAsync(ct);
    }
}
