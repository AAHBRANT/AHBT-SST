using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Commands;

public record CriarCursoTreinamentoCommand(
    string Nome,
    string? NormaReferencia,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico = null,
    bool EhIntegracaoSeguranca = false,
    bool AtendeNr6 = false) : IRequest<Guid>;

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
            EhIntegracaoSeguranca = request.EhIntegracaoSeguranca,
            AtendeNr6 = request.AtendeNr6,
        };

        if (request.EhIntegracaoSeguranca)
        {
            var atual = await _db.CursosTreinamento.Where(c => c.EhIntegracaoSeguranca).ToListAsync(ct);
            foreach (var c in atual) c.EhIntegracaoSeguranca = false;
        }

        _db.CursosTreinamento.Add(curso);
        await _db.SaveChangesAsync(ct);
        return curso.Id;
    }
}
