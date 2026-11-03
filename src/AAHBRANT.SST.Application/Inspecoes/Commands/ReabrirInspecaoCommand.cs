using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Válvula de escape de "Finalizar": enquanto a inspeção está Aguardando assinatura e ninguém assinou,
// ela pode voltar para Em andamento para corrigir um erro. Depois da primeira assinatura (ou de
// concluída), não reabre — o conteúdo assinado não pode mudar.
public record ReabrirInspecaoCommand(Guid Id) : IRequest;

public class ReabrirInspecaoCommandValidator : AbstractValidator<ReabrirInspecaoCommand>
{
    public ReabrirInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ReabrirInspecaoCommandHandler : IRequestHandler<ReabrirInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public ReabrirInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ReabrirInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");

        if (inspecao.Status != StatusInspecao.AguardandoAssinatura)
            throw new InvalidOperationException("Só é possível reabrir uma inspeção que está aguardando assinatura.");

        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.Inspecao) && d.EntidadeId == inspecao.Id, ct);

        if (documento is not null)
        {
            var jaTemAssinatura = await _db.DocumentoSignatarios.AnyAsync(s => s.DocumentoAssinaturaId == documento.Id, ct);
            if (jaTemAssinatura || documento.Status != StatusDocumentoAssinatura.EmAndamento)
                throw new InvalidOperationException("Esta inspeção já tem assinatura e não pode ser reaberta.");
        }

        inspecao.Status = StatusInspecao.EmAndamento;
        await _db.SaveChangesAsync(ct);
    }
}
