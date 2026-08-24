using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoRequisitos.Commands;

// "Requisitos" (§18) — checklist próprio desta PT (Descricao + Atendido), não vinculado a
// nenhum catálogo de TipoAutorizacao/Requisito (não existe tal entidade na base atual).
public record CriarPermissaoTrabalhoRequisitoCommand(Guid PermissaoTrabalhoId, string Descricao) : IRequest<Guid>;

public class CriarPermissaoTrabalhoRequisitoCommandValidator : AbstractValidator<CriarPermissaoTrabalhoRequisitoCommand>
{
    public CriarPermissaoTrabalhoRequisitoCommandValidator()
    {
        RuleFor(x => x.PermissaoTrabalhoId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
    }
}

public class CriarPermissaoTrabalhoRequisitoCommandHandler : IRequestHandler<CriarPermissaoTrabalhoRequisitoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPermissaoTrabalhoRequisitoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPermissaoTrabalhoRequisitoCommand request, CancellationToken ct)
    {
        var ptExiste = await _db.PermissoesTrabalho.AnyAsync(p => p.Id == request.PermissaoTrabalhoId, ct);
        if (!ptExiste)
            throw new KeyNotFoundException($"Permissão de Trabalho {request.PermissaoTrabalhoId} não encontrada.");

        var requisito = new PermissaoTrabalhoRequisito
        {
            PermissaoTrabalhoId = request.PermissaoTrabalhoId,
            Descricao = request.Descricao,
            Atendido = false
        };

        _db.PermissaoTrabalhoRequisitos.Add(requisito);
        await _db.SaveChangesAsync(ct);
        return requisito.Id;
    }
}
