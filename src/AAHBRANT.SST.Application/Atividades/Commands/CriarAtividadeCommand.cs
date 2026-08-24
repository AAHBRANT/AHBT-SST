using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Atividades.Commands;

public record CriarAtividadeCommand(Guid ObraId, string Nome, string? Descricao) : IRequest<Guid>;

public class CriarAtividadeCommandValidator : AbstractValidator<CriarAtividadeCommand>
{
    public CriarAtividadeCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
    }
}

public class CriarAtividadeCommandHandler : IRequestHandler<CriarAtividadeCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAtividadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAtividadeCommand request, CancellationToken ct)
    {
        var atividade = new Atividade
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            Descricao = request.Descricao
        };

        _db.Atividades.Add(atividade);
        await _db.SaveChangesAsync(ct);
        return atividade.Id;
    }
}
