using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record CriarFuncaoCommand(string Nome, string? CboCodigo, string? Descricao) : IRequest<Guid>;

public class CriarFuncaoCommandValidator : AbstractValidator<CriarFuncaoCommand>
{
    public CriarFuncaoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CboCodigo).MaximumLength(20);
    }
}

public class CriarFuncaoCommandHandler : IRequestHandler<CriarFuncaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarFuncaoCommand request, CancellationToken ct)
    {
        var funcao = new Funcao
        {
            Nome = request.Nome,
            CboCodigo = request.CboCodigo,
            Descricao = request.Descricao
        };

        _db.Funcoes.Add(funcao);
        await _db.SaveChangesAsync(ct);
        return funcao.Id;
    }
}
