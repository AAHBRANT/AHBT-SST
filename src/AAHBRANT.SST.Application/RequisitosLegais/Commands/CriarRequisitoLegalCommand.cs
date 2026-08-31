using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.RequisitosLegais.Commands;

// Cadastro do requisito em si (Norma/Artigo/Título/Descrição/Categoria/Fonte) — os critérios de
// aplicabilidade são definidos depois via DefinirCriteriosRequisitoLegalCommand, mesmo princípio de
// separação já usado em Funcao (cadastro) vs MatrizEpiFuncao (vínculo).
public record CriarRequisitoLegalCommand(
    string Norma,
    string? Artigo,
    string Titulo,
    string Descricao,
    CategoriaRequisitoLegal Categoria,
    string? Fonte) : IRequest<Guid>;

public class CriarRequisitoLegalCommandValidator : AbstractValidator<CriarRequisitoLegalCommand>
{
    public CriarRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Norma).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Artigo).MaximumLength(60);
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Fonte).MaximumLength(500);
    }
}

public class CriarRequisitoLegalCommandHandler : IRequestHandler<CriarRequisitoLegalCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = new RequisitoLegal
        {
            Norma = request.Norma,
            Artigo = request.Artigo,
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            Categoria = request.Categoria,
            Fonte = request.Fonte,
        };

        _db.RequisitosLegais.Add(requisito);
        await _db.SaveChangesAsync(ct);
        return requisito.Id;
    }
}
