using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Inspeções de Tema/Veículo/Alojamento nascem com o responsável = quem abriu (ObterOuCriar...), mas
// quem cria nem sempre é o responsável (30/10/2026). A troca vale até a inspeção ser assinada —
// depois disso o PDF está congelado (ProtecaoDocumentoAssinado), mesmo que já esteja Concluída.
public record AlterarResponsavelInspecaoCommand(Guid Id, Guid ResponsavelUsuarioId) : IRequest;

public class AlterarResponsavelInspecaoCommandValidator : AbstractValidator<AlterarResponsavelInspecaoCommand>
{
    public AlterarResponsavelInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ResponsavelUsuarioId).NotEmpty();
    }
}

public class AlterarResponsavelInspecaoCommandHandler : IRequestHandler<AlterarResponsavelInspecaoCommand>
{
    private readonly IAppDbContext _db;
    public AlterarResponsavelInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AlterarResponsavelInspecaoCommand request, CancellationToken ct)
    {
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, "Inspecao", request.Id, ct);

        var inspecao = await _db.Inspecoes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Inspeção não encontrada.");

        if (!await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        inspecao.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        await _db.SaveChangesAsync(ct);
    }
}
