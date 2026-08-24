using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Commands;

public record ExcluirCursoTreinamentoCommand(Guid Id) : IRequest;

public class ExcluirCursoTreinamentoCommandValidator : AbstractValidator<ExcluirCursoTreinamentoCommand>
{
    public ExcluirCursoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirCursoTreinamentoCommandHandler : IRequestHandler<ExcluirCursoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    public ExcluirCursoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirCursoTreinamentoCommand request, CancellationToken ct)
    {
        var curso = await _db.CursosTreinamento.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Curso de treinamento não encontrado.");

        _db.CursosTreinamento.Remove(curso);
        await _db.SaveChangesAsync(ct);
    }
}
