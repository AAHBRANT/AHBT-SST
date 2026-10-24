using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// O navegador pede um desafio antes de cada leitura e o repassa ao agente, que o assina junto com o
// resultado. Vale por pouco tempo e uma vez só (ver FutronicAutenticacaoStrategy).
public record EmitirDesafioBiometriaCommand(Guid DispositivoId) : IRequest<string>;

public class EmitirDesafioBiometriaCommandValidator : AbstractValidator<EmitirDesafioBiometriaCommand>
{
    public EmitirDesafioBiometriaCommandValidator() => RuleFor(x => x.DispositivoId).NotEmpty();
}

public class EmitirDesafioBiometriaCommandHandler : IRequestHandler<EmitirDesafioBiometriaCommand, string>
{
    // Cobre a espera do dedo no leitor (até 120 s no agente) com folga.
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(3);

    private readonly IAppDbContext _db;

    public EmitirDesafioBiometriaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<string> Handle(EmitirDesafioBiometriaCommand request, CancellationToken ct)
    {
        if (!await _db.DispositivosAgenteBiometrico.AnyAsync(d => d.Id == request.DispositivoId, ct))
            throw new KeyNotFoundException("Leitor de digital não encontrado ou revogado.");

        var agora = DateTime.UtcNow;
        var desafio = new DesafioBiometria { DispositivoId = request.DispositivoId, ExpiraEm = agora.Add(Validade) };
        _db.DesafiosBiometria.Add(desafio);

        // Limpeza dos vencidos há mais de um dia, aproveitando a própria emissão (são poucos por dia).
        var limite = agora.AddDays(-1);
        _db.DesafiosBiometria.RemoveRange(await _db.DesafiosBiometria.Where(d => d.ExpiraEm < limite).Take(500).ToListAsync(ct));

        await _db.SaveChangesAsync(ct);
        return desafio.Id.ToString("N");
    }
}
