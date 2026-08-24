using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Commands;

public record AtualizarAsoCommand(
    Guid Id,
    Guid TrabalhadorId,
    TipoExameAso Tipo,
    DateTime DataExame,
    DateTime DataValidade,
    ResultadoAso ResultadoStatus,
    string? MedicoNome,
    string? MedicoCrm,
    string? ObservacoesClinicas) : IRequest;

public class AtualizarAsoCommandValidator : AbstractValidator<AtualizarAsoCommand>
{
    public AtualizarAsoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DataExame).NotEmpty();
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThanOrEqualTo(x => x.DataExame);
    }
}

public class AtualizarAsoCommandHandler : IRequestHandler<AtualizarAsoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarAsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarAsoCommand request, CancellationToken ct)
    {
        var aso = await _db.Asos.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"ASO {request.Id} não encontrado.");

        aso.TrabalhadorId = request.TrabalhadorId;
        aso.Tipo = request.Tipo;
        aso.DataExame = request.DataExame;
        aso.DataValidade = request.DataValidade;
        aso.ResultadoStatus = request.ResultadoStatus;
        aso.MedicoNome = request.MedicoNome;
        aso.MedicoCrm = request.MedicoCrm;
        aso.ObservacoesClinicas = request.ObservacoesClinicas;

        await _db.SaveChangesAsync(ct);
    }
}
