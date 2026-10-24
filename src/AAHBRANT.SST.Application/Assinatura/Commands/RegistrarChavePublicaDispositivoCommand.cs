using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Chamado pelo próprio agente (não pelo navegador), autenticado pelo segredo do dispositivo. A chave é
// gravada no primeiro registro e não muda mais: se outra chave aparecer para o mesmo leitor (agente
// reinstalado ou alguém tentando se passar por ele), é recusada — a saída é registrar o leitor de novo
// em Administração > Leitores de digital.
public record RegistrarChavePublicaDispositivoCommand(Guid DispositivoId, string SegredoDispositivo, string ChavePublica) : IRequest;

public class RegistrarChavePublicaDispositivoCommandValidator : AbstractValidator<RegistrarChavePublicaDispositivoCommand>
{
    public RegistrarChavePublicaDispositivoCommandValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.ChavePublica).NotEmpty().MaximumLength(512)
            .Must(EhChaveEcdsaValida).WithMessage("Chave pública inválida.");
    }

    private static bool EhChaveEcdsaValida(string chave)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(chave), out _);
            return true;
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return false;
        }
    }
}

public class RegistrarChavePublicaDispositivoCommandHandler : IRequestHandler<RegistrarChavePublicaDispositivoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _autenticador;

    public RegistrarChavePublicaDispositivoCommandHandler(IAppDbContext db, IDispositivoAgenteAutenticador autenticador)
    {
        _db = db;
        _autenticador = autenticador;
    }

    public async Task Handle(RegistrarChavePublicaDispositivoCommand request, CancellationToken ct)
    {
        var dispositivo = await _autenticador.ValidarAsync(request.DispositivoId, request.SegredoDispositivo, ct);
        if (dispositivo.ChavePublicaAssinatura == request.ChavePublica)
            return;
        if (dispositivo.ChavePublicaAssinatura is not null)
            throw new InvalidOperationException(
                "Este leitor já tem outra chave registrada. Se o agente foi reinstalado, registre o leitor de novo em Administração > Leitores de digital.");

        dispositivo.ChavePublicaAssinatura = request.ChavePublica;
        dispositivo.ChavePublicaRegistradaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
