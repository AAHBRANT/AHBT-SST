using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.CursosTreinamento.Commands;

public record CriarCursoTreinamentoCommand(
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico = null) : IRequest<Guid>;

public class CriarCursoTreinamentoCommandValidator : AbstractValidator<CriarCursoTreinamentoCommand>
{
    public CriarCursoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.CargaHorariaMinima).GreaterThan(0);
        RuleFor(x => x.ValidadeEmMeses).GreaterThan(0);
    }
}

public class CriarCursoTreinamentoCommandHandler : IRequestHandler<CriarCursoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCursoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCursoTreinamentoCommand request, CancellationToken ct)
    {
        var curso = new Domain.Entidades.CursoTreinamento
        {
            Nome = request.Nome,
            NormaReferencia = request.NormaReferencia,
            CargaHorariaMinima = request.CargaHorariaMinima,
            ValidadeEmMeses = request.ValidadeEmMeses,
            ConteudoProgramatico = request.ConteudoProgramatico,
        };
        _db.CursosTreinamento.Add(curso);
        await _db.SaveChangesAsync(ct);
        return curso.Id;
    }
}
