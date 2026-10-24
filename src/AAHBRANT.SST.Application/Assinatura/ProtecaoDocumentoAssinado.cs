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

    // Regra de cada módulo: quais documentos de assinatura contam e quando travam. Fonte única para os comandos
    // (que recusam a alteração) e para a tela (que esconde os botões de editar/excluir, ver ListarTravadosAsync).
    public static (string[] TiposDocumento, Trava Trava)? RegraPara(string entidadeTipo) => entidadeTipo switch
    {
        "EntregaEpi" => (["EntregaEpi", "DevolucaoEpi"], Trava.AposPrimeiraAssinatura),
        "EntregaUniforme" => (["EntregaUniforme"], Trava.AposPrimeiraAssinatura),
        "Inspecao" => (["Inspecao"], Trava.AposPrimeiraAssinatura),
        "Apr" => (["Apr"], Trava.AposFinalizacao),
        "PermissaoTrabalho" => (["PermissaoTrabalho"], Trava.AposFinalizacao),
        "Treinamento" => (["Treinamento"], Trava.AposFinalizacao),
        "Dds" => (["Dds"], Trava.AposFinalizacao),
        _ => null,
    };

    public static Task GarantirEditavelAsync(IAppDbContext db, string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        var regra = RegraPara(entidadeTipo) ?? throw new ArgumentException($"Sem regra de proteção para {entidadeTipo}.", nameof(entidadeTipo));
        return GarantirEditavelAsync(db, entidadeId, regra.Trava, ct, regra.TiposDocumento);
    }

    // Dos ids informados, quais já estão travados. Uma consulta só, para a tela marcar a lista inteira.
    public static async Task<HashSet<Guid>> ListarTravadosAsync(IAppDbContext db, string entidadeTipo, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (RegraPara(entidadeTipo) is not { } regra || ids.Count == 0)
            return new HashSet<Guid>();
        var tipos = regra.TiposDocumento;
        var documentos = db.DocumentosAssinatura.IgnoreQueryFilters()
            .Where(d => ids.Contains(d.EntidadeId) && tipos.Contains(d.EntidadeTipo));
        var travados = regra.Trava == Trava.AposFinalizacao
            ? documentos.Where(d => d.Status == StatusDocumentoAssinatura.Finalizado)
            : documentos.Where(d => d.Status == StatusDocumentoAssinatura.Finalizado || db.DocumentoSignatarios.Any(s => s.DocumentoAssinaturaId == d.Id));
        return (await travados.Select(d => d.EntidadeId).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public static async Task<bool> EstaTravadoAsync(IAppDbContext db, Guid entidadeId, Trava trava, CancellationToken ct, params string[] entidadeTipos)
    {
        var documentos = db.DocumentosAssinatura.IgnoreQueryFilters()
            .Where(d => d.EntidadeId == entidadeId && entidadeTipos.Contains(d.EntidadeTipo));

        return trava == Trava.AposFinalizacao
            ? await documentos.AnyAsync(d => d.Status == StatusDocumentoAssinatura.Finalizado, ct)
            : await documentos.AnyAsync(d => d.Status == StatusDocumentoAssinatura.Finalizado
                || db.DocumentoSignatarios.Any(s => s.DocumentoAssinaturaId == d.Id), ct);
    }

    public static async Task GarantirEditavelAsync(IAppDbContext db, Guid entidadeId, Trava trava, CancellationToken ct, params string[] entidadeTipos)
    {
        if (await EstaTravadoAsync(db, entidadeId, trava, ct, entidadeTipos))
            throw new InvalidOperationException(trava == Trava.AposFinalizacao
                ? "Este documento já foi finalizado e não pode mais ser alterado nem excluído. Para corrigir, registre um novo."
                : "Este registro já foi assinado e não pode mais ser alterado nem excluído. Para corrigir, registre um novo.");
    }
}
