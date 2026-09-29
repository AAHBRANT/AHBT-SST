using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// O segredo em claro só existe neste retorno: no banco fica apenas o hash. Quem registra precisa
// guardar o segredo (e o Id) agora para configurar o agente no PC da obra.
public record RegistroDispositivoAgente(Guid DispositivoId, string Segredo);

public record RegistrarDispositivoAgenteCommand(Guid ObraId, string Nome) : IRequest<RegistroDispositivoAgente>;

public class RegistrarDispositivoAgenteCommandValidator : AbstractValidator<RegistrarDispositivoAgenteCommand>
{
    public RegistrarDispositivoAgenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
    }
}

public class RegistrarDispositivoAgenteCommandHandler : IRequestHandler<RegistrarDispositivoAgenteCommand, RegistroDispositivoAgente>
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public RegistrarDispositivoAgenteCommandHandler(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<RegistroDispositivoAgente> Handle(RegistrarDispositivoAgenteCommand request, CancellationToken ct)
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
            Nome = request.Nome.Trim(),
            SegredoHash = _hasher.GerarHash(segredo),
        };
        _db.DispositivosAgenteBiometrico.Add(dispositivo);
        await _db.SaveChangesAsync(ct);

        return new RegistroDispositivoAgente(dispositivo.Id, segredo);
    }
}
