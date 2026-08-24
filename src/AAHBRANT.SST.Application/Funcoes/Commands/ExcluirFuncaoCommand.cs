using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record ExcluirFuncaoCommand(Guid Id) : IRequest;

public class ExcluirFuncaoCommandValidator : AbstractValidator<ExcluirFuncaoCommand>
{
    public ExcluirFuncaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirFuncaoCommandHandler : IRequestHandler<ExcluirFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirFuncaoCommand request, CancellationToken ct)
    {
        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Função {request.Id} não encontrada.");

        _db.Funcoes.Remove(funcao);
        await _db.SaveChangesAsync(ct);
    }
}
