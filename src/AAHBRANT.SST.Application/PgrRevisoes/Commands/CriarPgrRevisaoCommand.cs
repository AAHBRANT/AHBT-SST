using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PgrRevisoes.Commands;

// Revisão do PGR (§16) é append-only por natureza — não há Atualizar/Excluir, só registro
// incremental (mesmo princípio da TrilhaAuditoria, sem UPDATE/DELETE a nível de negócio).
public record CriarPgrRevisaoCommand(
    Guid PgrId,
    DateTime DataRevisao,
    string Motivo,
    Guid? ResponsavelUsuarioId) : IRequest<Guid>;

public class CriarPgrRevisaoCommandValidator : AbstractValidator<CriarPgrRevisaoCommand>
{
    public CriarPgrRevisaoCommandValidator()
    {
        RuleFor(x => x.PgrId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}

public class CriarPgrRevisaoCommandHandler : IRequestHandler<CriarPgrRevisaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPgrRevisaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPgrRevisaoCommand request, CancellationToken ct)
    {
        var ultimoNumero = await _db.PgrRevisoes
            .Where(r => r.PgrId == request.PgrId)
            .OrderByDescending(r => r.NumeroRevisao)
            .Select(r => (int?)r.NumeroRevisao)
            .FirstOrDefaultAsync(ct) ?? 0;

        var revisao = new PgrRevisao
        {
            PgrId = request.PgrId,
            NumeroRevisao = ultimoNumero + 1,
            DataRevisao = request.DataRevisao,
            Motivo = request.Motivo,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId
        };

        _db.PgrRevisoes.Add(revisao);
        await _db.SaveChangesAsync(ct);
        return revisao.Id;
    }
}
