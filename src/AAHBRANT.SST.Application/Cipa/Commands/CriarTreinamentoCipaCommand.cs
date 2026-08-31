using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record CriarTreinamentoCipaCommand(
    Guid MembroCipaId,
    int CargaHoraria,
    string? ConteudoProgramatico,
    DateTime DataRealizacao,
    DateTime? DataValidade,
    string? InstituicaoInstrutor) : IRequest<Guid>;

public class CriarTreinamentoCipaCommandValidator : AbstractValidator<CriarTreinamentoCipaCommand>
{
    public CriarTreinamentoCipaCommandValidator()
    {
        RuleFor(x => x.MembroCipaId).NotEmpty();
        RuleFor(x => x.CargaHoraria).GreaterThan(0);
        RuleFor(x => x.ConteudoProgramatico).MaximumLength(2000);
        RuleFor(x => x.InstituicaoInstrutor).MaximumLength(200);
    }
}

public class CriarTreinamentoCipaCommandHandler : IRequestHandler<CriarTreinamentoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarTreinamentoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTreinamentoCipaCommand request, CancellationToken ct)
    {
        if (!await _db.MembrosCipa.AnyAsync(m => m.Id == request.MembroCipaId, ct))
            throw new KeyNotFoundException($"Membro da CIPA {request.MembroCipaId} não encontrado.");

        var treinamento = new TreinamentoCipa
        {
            MembroCipaId = request.MembroCipaId,
            CargaHoraria = request.CargaHoraria,
            ConteudoProgramatico = request.ConteudoProgramatico,
            DataRealizacao = request.DataRealizacao,
            DataValidade = request.DataValidade,
            InstituicaoInstrutor = request.InstituicaoInstrutor,
        };

        _db.TreinamentosCipa.Add(treinamento);
        await _db.SaveChangesAsync(ct);
        return treinamento.Id;
    }
}
