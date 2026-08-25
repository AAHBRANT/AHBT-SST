using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.RegrasAlerta.Commands;

public record CriarRegraAlertaCommand(
    TipoModuloAlerta Modulo,
    int DiasAntecedencia,
    SeveridadeAlerta Severidade,
    Guid? ResponsavelUsuarioId = null) : IRequest<Guid>;

public class CriarRegraAlertaCommandValidator : AbstractValidator<CriarRegraAlertaCommand>
{
    public CriarRegraAlertaCommandValidator()
    {
        RuleFor(x => x.DiasAntecedencia).GreaterThanOrEqualTo(0);
    }
}

public class CriarRegraAlertaCommandHandler : IRequestHandler<CriarRegraAlertaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRegraAlertaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRegraAlertaCommand request, CancellationToken ct)
    {
        var regra = new RegraAlerta
        {
            Modulo = request.Modulo,
            DiasAntecedencia = request.DiasAntecedencia,
            Severidade = request.Severidade,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId
        };

        _db.RegrasAlerta.Add(regra);
        await _db.SaveChangesAsync(ct);
        return regra.Id;
    }
}
