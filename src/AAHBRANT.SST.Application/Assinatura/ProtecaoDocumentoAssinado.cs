using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Registro assinado não pode mais ser alterado nem excluído (12/10): antes a entrega de EPI assinada podia
// ser editada (item, quantidade, data) ou apagada, e a assinatura passava a "provar" um conteúdo que o
// funcionário nunca viu (auditoria de 06/10). Para corrigir, registra-se um novo.
//   - AposPrimeiraAssinatura: documentos de uma pessoa só (entrega/devolução de EPI, uniforme, inspeção) —
//     travam assim que alguém assina.
//   - AposFinalizacao: documentos coletivos (APR, PT, DDS, certificado de treinamento) — continuam recebendo
//     assinaturas e só travam quando o documento é finalizado.
public static class ProtecaoDocumentoAssinado
{
    public enum Trava { AposPrimeiraAssinatura, AposFinalizacao }

    public static async Task GarantirEditavelAsync(IAppDbContext db, Guid entidadeId, Trava trava, CancellationToken ct, params string[] entidadeTipos)
    {
        var documentos = db.DocumentosAssinatura.IgnoreQueryFilters()
            .Where(d => d.EntidadeId == entidadeId && entidadeTipos.Contains(d.EntidadeTipo));

        var travado = trava == Trava.AposFinalizacao
            ? await documentos.AnyAsync(d => d.Status == StatusDocumentoAssinatura.Finalizado, ct)
            : await documentos.AnyAsync(d => d.Status == StatusDocumentoAssinatura.Finalizado
                || db.DocumentoSignatarios.Any(s => s.DocumentoAssinaturaId == d.Id), ct);

        if (travado)
            throw new InvalidOperationException(trava == Trava.AposFinalizacao
                ? "Este documento já foi finalizado e não pode mais ser alterado nem excluído. Para corrigir, registre um novo."
                : "Este registro já foi assinado e não pode mais ser alterado nem excluído. Para corrigir, registre um novo.");
    }
}
