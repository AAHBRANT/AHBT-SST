using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Asos.Commands;

public record CriarAsoCommand(
    Guid TrabalhadorId,
    TipoExameAso Tipo,
    DateTime DataExame,
    DateTime DataValidade,
    ResultadoAso ResultadoStatus,
    string? MedicoNome,
    string? MedicoCrm,
    string? ObservacoesClinicas) : IRequest<Guid>;

public class CriarAsoCommandValidator : AbstractValidator<CriarAsoCommand>
{
    public CriarAsoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataExame).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataExame);
    }
}

public class CriarAsoCommandHandler : IRequestHandler<CriarAsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarAsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarAsoCommand request, CancellationToken ct)
    {
        var aso = new Aso
        {
            TrabalhadorId = request.TrabalhadorId,
            Tipo = request.Tipo,
            DataExame = request.DataExame,
            DataValidade = request.DataValidade,
            ResultadoStatus = request.ResultadoStatus,
            MedicoNome = request.MedicoNome,
            MedicoCrm = request.MedicoCrm,
            ObservacoesClinicas = request.ObservacoesClinicas
        };

        _db.Asos.Add(aso);
        await _db.SaveChangesAsync(ct);
        return aso.Id;
    }
}
