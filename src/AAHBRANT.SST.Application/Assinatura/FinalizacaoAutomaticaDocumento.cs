using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Assinatura;

// Fecha o documento de assinatura nos fins naturais de cada fluxo (inspeção assinada, DDS encerrado,
// turma encerrada). Até 11/10 nenhum documento era finalizado na prática: ficavam "em andamento" para
// sempre, aceitando assinaturas e com o hash e o PDF refeitos a cada exportação. Melhor esforço: o fluxo
// que chamou (encerrar o DDS, por exemplo) já está gravado e não pode falhar por causa disto.
public static class FinalizacaoAutomaticaDocumento
{
    public static async Task TentarAsync(IAppDbContext db, IMediator mediator, ILogger logger, string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        var documentoId = await db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == entidadeTipo && d.EntidadeId == entidadeId && d.Status == StatusDocumentoAssinatura.EmAndamento)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (documentoId is null)
            return;
        await TentarAsync(db, mediator, logger, documentoId.Value, ct);
    }

    public static async Task TentarAsync(IAppDbContext db, IMediator mediator, ILogger logger, Guid documentoId, CancellationToken ct)
    {
        // Sem nenhuma assinatura não há o que finalizar (o comando recusaria).
        if (!await db.DocumentoSignatarios.AnyAsync(s => s.DocumentoAssinaturaId == documentoId, ct))
            return;
        try
        {
            await mediator.Send(new FinalizarDocumentoCommand(documentoId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível finalizar automaticamente o documento de assinatura {DocumentoId}.", documentoId);
        }
    }
}
