using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

// Edição de dados de cadastro (local/equipe/datas/responsáveis) — não altera Status/aprovação,
// que passam por AprovarAprCommand/ReprovarAprCommand.
public record AtualizarAprCommand(
    Guid Id,
    Guid AtividadeId,
    string Local,
    Guid? EquipeId,
    DateTime Data,
    DateTime? Validade,
    List<Guid> ResponsaveisIds) : IRequest;

public class AtualizarAprCommandValidator : AbstractValidator<AtualizarAprCommand>
{
    public AtualizarAprCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarAprCommandHandler : IRequestHandler<AtualizarAprCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAprCommand request, CancellationToken ct)
    {
        var apr = await _db.Aprs
            .Include(a => a.Responsaveis)
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"APR {request.Id} não encontrada.");

        apr.AtividadeId = request.AtividadeId;
        apr.Local = request.Local;
        apr.EquipeId = request.EquipeId;
        apr.Data = request.Data;
        apr.Validade = request.Validade;

        var idsNovos = request.ResponsaveisIds.Distinct().ToHashSet();
        foreach (var vinculoAntigo in apr.Responsaveis.Where(v => !idsNovos.Contains(v.TrabalhadorId)).ToList())
            _db.AprResponsaveis.Remove(vinculoAntigo);

        var idsExistentes = apr.Responsaveis.Select(v => v.TrabalhadorId).ToHashSet();
        foreach (var trabalhadorId in idsNovos.Where(id => !idsExistentes.Contains(id)))
            apr.Responsaveis.Add(new AprResponsavel { AprId = apr.Id, TrabalhadorId = trabalhadorId });

        await _db.SaveChangesAsync(ct);
    }
}
