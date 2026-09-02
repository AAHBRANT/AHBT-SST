using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Catálogo pré-cadastrado de temas de DDS (31/08) — tema livre opcional, somado aos temas
// automáticos das atividades do dia (01/09), nunca os substitui. Cadastro simples, sem
// versionamento — mesmo espírito de CatalogoEpi.
public record CriarCatalogoTemaDdsCommand(string Nome, string? Descricao) : IRequest<Guid>;

public class CriarCatalogoTemaDdsCommandValidator : AbstractValidator<CriarCatalogoTemaDdsCommand>
{
    public CriarCatalogoTemaDdsCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(500);
    }
}

public class CriarCatalogoTemaDdsCommandHandler : IRequestHandler<CriarCatalogoTemaDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarCatalogoTemaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCatalogoTemaDdsCommand request, CancellationToken ct)
    {
        var tema = new CatalogoTemaDds { Nome = request.Nome, Descricao = request.Descricao };
        _db.CatalogosTemaDds.Add(tema);
        await _db.SaveChangesAsync(ct);
        return tema.Id;
    }
}
