using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RequisitosLegais.Commands;

public record AtualizarRequisitoLegalCommand(
    Guid Id,
    string Norma,
    string? Artigo,
    string Titulo,
    string Descricao,
    CategoriaRequisitoLegal Categoria,
    StatusRequisitoLegal Status,
    string? Fonte) : IRequest;

public class AtualizarRequisitoLegalCommandValidator : AbstractValidator<AtualizarRequisitoLegalCommand>
{
    public AtualizarRequisitoLegalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Norma).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Artigo).MaximumLength(60);
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Fonte).MaximumLength(500);
    }
}

public class AtualizarRequisitoLegalCommandHandler : IRequestHandler<AtualizarRequisitoLegalCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRequisitoLegalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRequisitoLegalCommand request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");

        requisito.Norma = request.Norma;
        requisito.Artigo = request.Artigo;
        requisito.Titulo = request.Titulo;
        requisito.Descricao = request.Descricao;
        requisito.Categoria = request.Categoria;
        requisito.Status = request.Status;
        requisito.Fonte = request.Fonte;

        await _db.SaveChangesAsync(ct);
    }
}
