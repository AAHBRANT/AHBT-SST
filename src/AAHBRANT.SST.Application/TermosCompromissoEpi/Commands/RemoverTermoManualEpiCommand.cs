using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi.Commands;

// Só o Administrador chama (a policy está no controller). A remoção é lógica e fica na auditoria.
public record RemoverTermoManualEpiCommand(Guid TrabalhadorId, Guid UsuarioId) : IRequest;

public class RemoverTermoManualEpiCommandValidator : AbstractValidator<RemoverTermoManualEpiCommand>
{
    public RemoverTermoManualEpiCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}

public class RemoverTermoManualEpiCommandHandler : IRequestHandler<RemoverTermoManualEpiCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RemoverTermoManualEpiCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task Handle(RemoverTermoManualEpiCommand request, CancellationToken ct)
    {
        await _db.GarantirTrabalhadorNoEscopoAsync(request.TrabalhadorId, ct);

        var termo = await _db.TermosCompromissoEpiManual.FirstOrDefaultAsync(t => t.TrabalhadorId == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Este funcionário não tem termo registrado como assinado manualmente.");

        await _auditoria.RegistrarAsync(
            "TermoCompromissoEpi.RegistroManualRemovido",
            TermoCompromissoEpiConsulta.EntidadeTipo,
            request.TrabalhadorId,
            usuarioId: request.UsuarioId,
            trabalhadorId: request.TrabalhadorId,
            dadosDepois: new { termo.DataAssinaturaPapel },
            ct);

        _db.TermosCompromissoEpiManual.Remove(termo);
        await _db.SaveChangesAsync(ct);
    }
}
