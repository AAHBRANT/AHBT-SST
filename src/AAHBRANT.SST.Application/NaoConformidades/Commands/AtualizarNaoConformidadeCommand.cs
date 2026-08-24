using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

public record AtualizarNaoConformidadeCommand(
    Guid Id,
    OrigemNaoConformidade OrigemDeteccao,
    string? RequisitoRelacionado,
    string Descricao,
    string? Local,
    Guid? AtividadeId,
    Guid? RiscoId,
    Guid? ResponsavelUsuarioId,
    DateTime? Prazo) : IRequest;

public class AtualizarNaoConformidadeCommandValidator : AbstractValidator<AtualizarNaoConformidadeCommand>
{
    public AtualizarNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RequisitoRelacionado).MaximumLength(300);
        RuleFor(x => x.Local).MaximumLength(200);
    }
}

public class AtualizarNaoConformidadeCommandHandler : IRequestHandler<AtualizarNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        if (request.RiscoId.HasValue &&
            !await _db.Riscos.AnyAsync(r => r.Id == request.RiscoId, ct))
            throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        nc.OrigemDeteccao = request.OrigemDeteccao;
        nc.RequisitoRelacionado = request.RequisitoRelacionado;
        nc.Descricao = request.Descricao;
        nc.Local = request.Local;
        nc.AtividadeId = request.AtividadeId;
        nc.RiscoId = request.RiscoId;
        nc.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        nc.Prazo = request.Prazo;

        await _db.SaveChangesAsync(ct);
    }
}
