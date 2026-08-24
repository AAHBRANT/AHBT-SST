using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanoAcao.Commands;

public record ExcluirPlanoAcaoItemCommand(Guid Id) : IRequest;

public class ExcluirPlanoAcaoItemCommandValidator : AbstractValidator<ExcluirPlanoAcaoItemCommand>
{
    public ExcluirPlanoAcaoItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirPlanoAcaoItemCommandHandler : IRequestHandler<ExcluirPlanoAcaoItemCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirPlanoAcaoItemCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirPlanoAcaoItemCommand request, CancellationToken ct)
    {
        var item = await _db.PlanoAcaoItens.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Item de plano de ação {request.Id} não encontrado.");

        _db.PlanoAcaoItens.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
