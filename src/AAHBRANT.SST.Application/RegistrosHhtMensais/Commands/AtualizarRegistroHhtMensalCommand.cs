using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegistrosHhtMensais.Commands;

public record AtualizarRegistroHhtMensalCommand(Guid Id, Guid ObraId, int Ano, int Mes, int HorasHomemTrabalhadas)
    : IRequest;

public class AtualizarRegistroHhtMensalCommandValidator : AbstractValidator<AtualizarRegistroHhtMensalCommand>
{
    public AtualizarRegistroHhtMensalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.HorasHomemTrabalhadas).GreaterThanOrEqualTo(0);
    }
}

public class AtualizarRegistroHhtMensalCommandHandler : IRequestHandler<AtualizarRegistroHhtMensalCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarRegistroHhtMensalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarRegistroHhtMensalCommand request, CancellationToken ct)
    {
        var registro = await _db.RegistrosHhtMensais.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Registro de HHT {request.Id} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (await _db.RegistrosHhtMensais.AnyAsync(
                r => r.Id != request.Id && r.ObraId == request.ObraId && r.Ano == request.Ano && r.Mes == request.Mes, ct))
            throw new InvalidOperationException("Já existe um registro de HHT para esta obra neste mês.");

        registro.ObraId = request.ObraId;
        registro.Ano = request.Ano;
        registro.Mes = request.Mes;
        registro.HorasHomemTrabalhadas = request.HorasHomemTrabalhadas;

        await _db.SaveChangesAsync(ct);
    }
}
