using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Commands;

public record AtualizarCursoTreinamentoCommand(
    Guid Id,
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses) : IRequest;

public class AtualizarCursoTreinamentoCommandValidator : AbstractValidator<AtualizarCursoTreinamentoCommand>
{
    public AtualizarCursoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty();
        RuleFor(x => x.CargaHorariaMinima).GreaterThan(0);
        RuleFor(x => x.ValidadeEmMeses).GreaterThan(0);
    }
}

public class AtualizarCursoTreinamentoCommandHandler : IRequestHandler<AtualizarCursoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCursoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCursoTreinamentoCommand request, CancellationToken ct)
    {
        var curso = await _db.CursosTreinamento.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Curso de treinamento não encontrado.");

        curso.Nome = request.Nome;
        curso.NormaReferencia = request.NormaReferencia;
        curso.CargaHorariaMinima = request.CargaHorariaMinima;
        curso.ValidadeEmMeses = request.ValidadeEmMeses;

        await _db.SaveChangesAsync(ct);
    }
}
