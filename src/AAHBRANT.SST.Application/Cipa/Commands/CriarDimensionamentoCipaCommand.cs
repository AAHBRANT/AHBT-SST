using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// NumeroTitulares/NumeroSuplentes são sempre informados por quem faz o dimensionamento — este
// sistema não calcula automaticamente o Quadro I da NR-5 (ver disclosure em Domain/Entidades/Cipa/Cipa.cs).
public record CriarDimensionamentoCipaCommand(
    Guid ObraId,
    string Cnae,
    int GrauRisco,
    int NumeroFuncionarios,
    int NumeroTitulares,
    int NumeroSuplentes,
    string? Observacoes) : IRequest<Guid>;

public class CriarDimensionamentoCipaCommandValidator : AbstractValidator<CriarDimensionamentoCipaCommand>
{
    public CriarDimensionamentoCipaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Cnae).NotEmpty().MaximumLength(20);
        RuleFor(x => x.GrauRisco).InclusiveBetween(1, 4);
        RuleFor(x => x.NumeroFuncionarios).GreaterThan(0);
        RuleFor(x => x.NumeroTitulares).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NumeroSuplentes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Observacoes).MaximumLength(1000);
    }
}

public class CriarDimensionamentoCipaCommandHandler : IRequestHandler<CriarDimensionamentoCipaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDimensionamentoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDimensionamentoCipaCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var dimensionamento = new DimensionamentoCipa
        {
            ObraId = request.ObraId,
            Cnae = request.Cnae,
            GrauRisco = request.GrauRisco,
            NumeroFuncionarios = request.NumeroFuncionarios,
            NumeroTitulares = request.NumeroTitulares,
            NumeroSuplentes = request.NumeroSuplentes,
            DataCalculo = DateTime.UtcNow,
            Observacoes = request.Observacoes,
        };

        _db.DimensionamentosCipa.Add(dimensionamento);
        await _db.SaveChangesAsync(ct);
        return dimensionamento.Id;
    }
}
