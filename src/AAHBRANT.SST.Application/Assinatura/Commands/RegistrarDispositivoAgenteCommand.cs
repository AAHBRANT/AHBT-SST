using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record RegistrarDispositivoAgenteCommand(Guid ObraId, string Nome) : IRequest<string>;

public class RegistrarDispositivoAgenteCommandValidator : AbstractValidator<RegistrarDispositivoAgenteCommand>
{
    public RegistrarDispositivoAgenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class RegistrarDispositivoAgenteCommandHandler : IRequestHandler<RegistrarDispositivoAgenteCommand, string>
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public RegistrarDispositivoAgenteCommandHandler(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<string> Handle(RegistrarDispositivoAgenteCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.ObraId, ct);
        if (obra is null)
        {
            throw new KeyNotFoundException("Obra não encontrada.");
        }

        var segredo = _hasher.GerarSegredo();
        var dispositivo = new DispositivoAgenteBiometrico
        {
            ObraId = request.ObraId,
            Nome = request.Nome,
            SegredoHash = _hasher.GerarHash(segredo),
        };
        _db.DispositivosAgenteBiometrico.Add(dispositivo);
        await _db.SaveChangesAsync(ct);

        return segredo;
    }
}
