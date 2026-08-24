using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record AtualizarFuncaoCommand(Guid Id, string Nome, string? CboCodigo, string? Descricao) : IRequest;

public class AtualizarFuncaoCommandValidator : AbstractValidator<AtualizarFuncaoCommand>
{
    public AtualizarFuncaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CboCodigo).MaximumLength(20);
    }
}

public class AtualizarFuncaoCommandHandler : IRequestHandler<AtualizarFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarFuncaoCommand request, CancellationToken ct)
    {
        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Função {request.Id} não encontrada.");

        funcao.Nome = request.Nome;
        funcao.CboCodigo = request.CboCodigo;
        funcao.Descricao = request.Descricao;

        await _db.SaveChangesAsync(ct);
    }
}
