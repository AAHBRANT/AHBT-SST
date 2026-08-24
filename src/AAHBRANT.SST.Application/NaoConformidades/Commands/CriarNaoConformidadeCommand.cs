using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

public record CriarNaoConformidadeCommand(
    OrigemNaoConformidade OrigemDeteccao,
    string? RequisitoRelacionado,
    string Descricao,
    string? Local,
    Guid? AtividadeId,
    Guid? RiscoId,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo) : IRequest<Guid>;

public class CriarNaoConformidadeCommandValidator : AbstractValidator<CriarNaoConformidadeCommand>
{
    public CriarNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RequisitoRelacionado).MaximumLength(300);
        RuleFor(x => x.Local).MaximumLength(200);
    }
}

public class CriarNaoConformidadeCommandHandler : IRequestHandler<CriarNaoConformidadeCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarNaoConformidadeCommand request, CancellationToken ct)
    {
        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        if (request.RiscoId.HasValue &&
            !await _db.Riscos.AnyAsync(r => r.Id == request.RiscoId, ct))
            throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        var nc = new NaoConformidade
        {
            OrigemDeteccao = request.OrigemDeteccao,
            RequisitoRelacionado = request.RequisitoRelacionado,
            Descricao = request.Descricao,
            Local = request.Local,
            AtividadeId = request.AtividadeId,
            RiscoId = request.RiscoId,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Prazo = request.Prazo,
        };

        _db.NaoConformidades.Add(nc);
        await _db.SaveChangesAsync(ct);
        return nc.Id;
    }
}
