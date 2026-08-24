using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

public record ExcluirTagIdentificacaoCommand(Guid Id) : IRequest;

public class ExcluirTagIdentificacaoCommandValidator : AbstractValidator<ExcluirTagIdentificacaoCommand>
{
    public ExcluirTagIdentificacaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirTagIdentificacaoCommandHandler : IRequestHandler<ExcluirTagIdentificacaoCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirTagIdentificacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirTagIdentificacaoCommand request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Tag {request.Id} não encontrada.");

        _db.TagsIdentificacao.Remove(tag);
        await _db.SaveChangesAsync(ct);
    }
}
