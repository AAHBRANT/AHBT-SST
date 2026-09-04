using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// Edição de dados de cadastro (cabeçalho/responsáveis) — não altera Status/autorização/suspensão/
// revalidação/encerramento, que passam por seus próprios comandos dedicados.
public record AtualizarPermissaoTrabalhoCommand(
    Guid Id,
    Guid AtividadeId,
    string DescricaoAtividade,
    string Local,
    string? EmpresaExecutante,
    Guid? EquipeId,
    DateTime Data,
    TimeSpan? HorarioInicio,
    TimeSpan? HorarioFim,
    DateTime? Validade,
    Guid? ResponsavelExecucaoUsuarioId,
    Guid? ResponsavelAreaUsuarioId,
    List<Guid> ResponsaveisIds) : IRequest;

public class AtualizarPermissaoTrabalhoCommandValidator : AbstractValidator<AtualizarPermissaoTrabalhoCommand>
{
    public AtualizarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.DescricaoAtividade).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmpresaExecutante).MaximumLength(200);
    }
}

public class AtualizarPermissaoTrabalhoCommandHandler : IRequestHandler<AtualizarPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho
            .Include(p => p.Responsaveis)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        pt.AtividadeId = request.AtividadeId;
        pt.DescricaoAtividade = request.DescricaoAtividade;
        pt.Local = request.Local;
        pt.EmpresaExecutante = request.EmpresaExecutante;
        pt.EquipeId = request.EquipeId;
        pt.Data = request.Data;
        pt.HorarioInicio = request.HorarioInicio;
        pt.HorarioFim = request.HorarioFim;
        pt.Validade = request.Validade;
        pt.ResponsavelExecucaoUsuarioId = request.ResponsavelExecucaoUsuarioId;
        pt.ResponsavelAreaUsuarioId = request.ResponsavelAreaUsuarioId;

        var responsaveisNovos = request.ResponsaveisIds.Distinct().ToHashSet();
        foreach (var vinculoAntigo in pt.Responsaveis.Where(v => !responsaveisNovos.Contains(v.TrabalhadorId)).ToList())
            _db.PermissaoTrabalhoResponsaveis.Remove(vinculoAntigo);
        var responsaveisExistentes = pt.Responsaveis.Select(v => v.TrabalhadorId).ToHashSet();
        foreach (var trabalhadorId in responsaveisNovos.Where(id => !responsaveisExistentes.Contains(id)))
            pt.Responsaveis.Add(new PermissaoTrabalhoResponsavel { PermissaoTrabalhoId = pt.Id, TrabalhadorId = trabalhadorId });

        await _db.SaveChangesAsync(ct);
    }
}
