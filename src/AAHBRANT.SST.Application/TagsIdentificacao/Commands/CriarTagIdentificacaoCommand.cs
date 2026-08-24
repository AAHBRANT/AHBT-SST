using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

public record CriarTagIdentificacaoCommand(string Uid, TipoTag Tipo) : IRequest<Guid>;

public class CriarTagIdentificacaoCommandValidator : AbstractValidator<CriarTagIdentificacaoCommand>
{
    public CriarTagIdentificacaoCommandValidator()
    {
        RuleFor(x => x.Uid).NotEmpty().MaximumLength(100);
    }
}

public class CriarTagIdentificacaoCommandHandler : IRequestHandler<CriarTagIdentificacaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarTagIdentificacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTagIdentificacaoCommand request, CancellationToken ct)
    {
        var jaExiste = await _db.TagsIdentificacao.AnyAsync(t => t.Uid == request.Uid, ct);
        if (jaExiste)
            throw new InvalidOperationException($"Já existe uma tag cadastrada com o UID {request.Uid}.");

        var tag = new TagIdentificacao
        {
            Uid = request.Uid,
            Tipo = request.Tipo,
            Status = StatusTag.Disponivel
        };

        _db.TagsIdentificacao.Add(tag);
        await _db.SaveChangesAsync(ct);
        return tag.Id;
    }
}
