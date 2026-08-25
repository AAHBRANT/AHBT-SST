using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PcmsoRevisoes.Commands;

// Revisão do PCMSO é append-only (mesmo princípio de PgrRevisao) — só registro incremental, sem
// Atualizar/Excluir.
public record CriarPcmsoRevisaoCommand(
    Guid PcmsoId,
    DateTime DataRevisao,
    string Motivo,
    Guid? ResponsavelUsuarioId) : IRequest<Guid>;

public class CriarPcmsoRevisaoCommandValidator : AbstractValidator<CriarPcmsoRevisaoCommand>
{
    public CriarPcmsoRevisaoCommandValidator()
    {
        RuleFor(x => x.PcmsoId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}

public class CriarPcmsoRevisaoCommandHandler : IRequestHandler<CriarPcmsoRevisaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPcmsoRevisaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPcmsoRevisaoCommand request, CancellationToken ct)
    {
        if (!await _db.Pcmsos.AnyAsync(p => p.Id == request.PcmsoId, ct))
            throw new KeyNotFoundException($"PCMSO {request.PcmsoId} não encontrado.");

        var ultimoNumero = await _db.PcmsoRevisoes
            .Where(r => r.PcmsoId == request.PcmsoId)
            .OrderByDescending(r => r.NumeroRevisao)
            .Select(r => (int?)r.NumeroRevisao)
            .FirstOrDefaultAsync(ct) ?? 0;

        var revisao = new PcmsoRevisao
        {
            PcmsoId = request.PcmsoId,
            NumeroRevisao = ultimoNumero + 1,
            DataRevisao = request.DataRevisao,
            Motivo = request.Motivo,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
        };

        _db.PcmsoRevisoes.Add(revisao);
        await _db.SaveChangesAsync(ct);
        return revisao.Id;
    }
}
