using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Commands;

// A APR sempre nasce em elaboração (§17 "aprovação" é uma etapa distinta do cadastro) — o campo
// Status não é exposto aqui; a mudança de status passa por AprovarAprCommand/ReprovarAprCommand.
public record CriarAprCommand(
    Guid AtividadeId,
    string Local,
    Guid? EquipeId,
    DateTime Data,
    DateTime? Validade,
    List<Guid> ResponsaveisIds) : IRequest<Guid>;

public class CriarAprCommandValidator : AbstractValidator<CriarAprCommand>
{
    public CriarAprCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
    }
}

public class CriarAprCommandHandler : IRequestHandler<CriarAprCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAprCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAprCommand request, CancellationToken ct)
    {
        var atividadeExiste = await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct);
        if (!atividadeExiste)
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var apr = new Apr
        {
            AtividadeId = request.AtividadeId,
            Local = request.Local,
            EquipeId = request.EquipeId,
            Data = request.Data,
            Validade = request.Validade,
        };

        foreach (var trabalhadorId in request.ResponsaveisIds.Distinct())
            apr.Responsaveis.Add(new AprResponsavel { TrabalhadorId = trabalhadorId });

        _db.Aprs.Add(apr);
        await _db.SaveChangesAsync(ct);
        return apr.Id;
    }
}
