using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        // Passa pelo filtro de obra: trabalhador de obra fora do escopo do usuário é "não encontrado"
        // (antes, um perfil restrito lançava ASO em trabalhador de qualquer obra, e um id inexistente
        // virava erro de FK/500).
        if (!await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct))
            throw new KeyNotFoundException("Trabalhador não encontrado.");

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
