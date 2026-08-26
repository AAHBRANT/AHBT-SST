using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record CriarRegistroHhtMensalCommand(Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas)
    : IRequest<Guid>;

public class CriarRegistroHhtMensalCommandValidator : AbstractValidator<CriarRegistroHhtMensalCommand>
{
    public CriarRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.HorasHomemTrabalhadas).GreaterThanOrEqualTo(0);
    }
}

public class CriarRegistroHhtMensalCommandHandler : IRequestHandler<CriarRegistroHhtMensalCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRegistroHhtMensalCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (await _db.RegistrosHhtMensais.AnyAsync(
                r => r.ObraId == request.ObraId && r.Ano == request.Ano && r.Mes == request.Mes, ct))
            throw new InvalidOperationException("Já existe um registro de HHT para esta obra neste mês.");

        var registro = new RegistroHhtMensal
        {
            ObraId = request.ObraId,
            Ano = request.Ano,
            Mes = request.Mes,
            HorasHomemTrabalhadas = request.HorasHomemTrabalhadas,
        };

        _db.RegistrosHhtMensais.Add(registro);
        await _db.SaveChangesAsync(ct);
        return registro.Id;
    }
}
