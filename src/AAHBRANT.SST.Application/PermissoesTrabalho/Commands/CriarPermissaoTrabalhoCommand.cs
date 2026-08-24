using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// A PT sempre nasce em elaboração (§18 "autorização" é uma etapa distinta do cadastro, mesmo
// padrão de CriarAprCommand) — Perigos/Responsáveis (catálogos existentes) entram aqui;
// Controles/Requisitos (conteúdo próprio da PT) entram via seus próprios módulos.
public record CriarPermissaoTrabalhoCommand(
    Guid AtividadeId,
    string Local,
    Guid? EquipeId,
    DateTime Data,
    TimeSpan? HorarioInicio,
    TimeSpan? HorarioFim,
    DateTime? Validade,
    List<Guid> PerigosIds,
    List<Guid> ResponsaveisIds) : IRequest<Guid>;

public class CriarPermissaoTrabalhoCommandValidator : AbstractValidator<CriarPermissaoTrabalhoCommand>
{
    public CriarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
    }
}

public class CriarPermissaoTrabalhoCommandHandler : IRequestHandler<CriarPermissaoTrabalhoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var atividadeExiste = await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct);
        if (!atividadeExiste)
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var pt = new PermissaoTrabalho
        {
            AtividadeId = request.AtividadeId,
            Local = request.Local,
            EquipeId = request.EquipeId,
            Data = request.Data,
            HorarioInicio = request.HorarioInicio,
            HorarioFim = request.HorarioFim,
            Validade = request.Validade,
        };

        foreach (var perigoId in request.PerigosIds.Distinct())
            pt.Perigos.Add(new PermissaoTrabalhoPerigo { PerigoId = perigoId });

        foreach (var trabalhadorId in request.ResponsaveisIds.Distinct())
            pt.Responsaveis.Add(new PermissaoTrabalhoResponsavel { TrabalhadorId = trabalhadorId });

        _db.PermissoesTrabalho.Add(pt);
        await _db.SaveChangesAsync(ct);
        return pt.Id;
    }
}
