using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Perigos.Commands;

public record CriarPerigoCommand(string Nome, string? Agente, string? Fonte, string? Descricao) : IRequest<Guid>;

public class CriarPerigoCommandValidator : AbstractValidator<CriarPerigoCommand>
{
    public CriarPerigoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class CriarPerigoCommandHandler : IRequestHandler<CriarPerigoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPerigoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPerigoCommand request, CancellationToken ct)
    {
        var perigo = new Perigo
        {
            Nome = request.Nome,
            Agente = request.Agente,
            Fonte = request.Fonte,
            Descricao = request.Descricao
        };

        _db.Perigos.Add(perigo);
        await _db.SaveChangesAsync(ct);
        return perigo.Id;
    }
}
