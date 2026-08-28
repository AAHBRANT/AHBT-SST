using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PcmsoItensMatriz.Commands;

public record CriarItemMatrizCommand(
    Guid PcmsoId,
    Guid FuncaoId,
    Guid? RiscoId,
    string NomeExame,
    int PeriodicidadeEmMeses,
    bool ObrigatorioNoAdmissional,
    bool ObrigatorioNoDemissional,
    string? Observacoes) : IRequest<Guid>;

public class CriarItemMatrizCommandValidator : AbstractValidator<CriarItemMatrizCommand>
{
    public CriarItemMatrizCommandValidator()
    {
        RuleFor(x => x.PcmsoId).NotEmpty();
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.NomeExame).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PeriodicidadeEmMeses).GreaterThan(0);
    }
}

public class CriarItemMatrizCommandHandler : IRequestHandler<CriarItemMatrizCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarItemMatrizCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarItemMatrizCommand request, CancellationToken ct)
    {
        if (!await _db.Pcmsos.AnyAsync(p => p.Id == request.PcmsoId, ct))
            throw new KeyNotFoundException($"PCMSO {request.PcmsoId} não encontrado.");

        if (!await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct))
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        if (request.RiscoId.HasValue && !await _db.Riscos.AnyAsync(r => r.Id == request.RiscoId, ct))
            throw new KeyNotFoundException($"Risco {request.RiscoId} não encontrado.");

        var item = new PcmsoItemMatriz
        {
            PcmsoId = request.PcmsoId,
            FuncaoId = request.FuncaoId,
            RiscoId = request.RiscoId,
            NomeExame = request.NomeExame,
            PeriodicidadeEmMeses = request.PeriodicidadeEmMeses,
            ObrigatorioNoAdmissional = request.ObrigatorioNoAdmissional,
            ObrigatorioNoDemissional = request.ObrigatorioNoDemissional,
            Observacoes = request.Observacoes,
        };

        _db.PcmsoItensMatriz.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}
