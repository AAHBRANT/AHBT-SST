using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi;

public enum SituacaoTermoCompromissoEpi
{
    Pendente = 0,
    // Assinado pelo próprio funcionário (digital ou facial) no Motor de Assinatura.
    Digital = 1,
    // Já assinado em papel; o sistema só registra a declaração de quem lançou.
    Manual = 2,
}

// DataAssinatura: no Digital é o instante da assinatura (UTC); no Manual é o dia que consta no
// papel. RegistradoPorNome/RegistradoEm só existem no Manual.
public record TermoCompromissoEpiDto(
    SituacaoTermoCompromissoEpi Situacao,
    DateTime? DataAssinatura,
    MetodoAutenticacaoAssinatura? Metodo,
    string? RegistradoPorNome,
    DateTime? RegistradoEm,
    string? Observacao,
    bool TemArquivo,
    string? ArquivoNome);

// Consulta única da situação do termo de um funcionário, usada pela tela e pelo PDF da ficha — as
// duas precisam enxergar a mesma verdade. O termo digital é um DocumentoAssinatura próprio do
// Motor (EntidadeTipo "TermoCompromissoEpi", EntidadeId = o trabalhador); o manual é um registro
// separado. Os dois nunca coexistem (o registro manual e o Motor se bloqueiam).
public static class TermoCompromissoEpiConsulta
{
    public const string EntidadeTipo = "TermoCompromissoEpi";

    public static async Task<TermoCompromissoEpiDto> ObterAsync(IAppDbContext db, Guid trabalhadorId, CancellationToken ct)
    {
        var digital = await db.DocumentoSignatarios
            .Where(s => s.TrabalhadorId == trabalhadorId
                && s.DocumentoAssinatura!.EntidadeTipo == EntidadeTipo
                && s.DocumentoAssinatura.EntidadeId == trabalhadorId)
            .OrderBy(s => s.AssinadoEm)
            .Select(s => new { s.AssinadoEm, s.MetodoAutenticacao })
            .FirstOrDefaultAsync(ct);
        if (digital is not null)
            return new TermoCompromissoEpiDto(SituacaoTermoCompromissoEpi.Digital, digital.AssinadoEm, digital.MetodoAutenticacao, null, null, null, false, null);

        // Projeta campo a campo: a entidade carrega o arquivo (bytes) e isso não pode vir junto.
        var manual = await db.TermosCompromissoEpiManual
            .Where(t => t.TrabalhadorId == trabalhadorId)
            .Select(t => new
            {
                t.DataAssinaturaPapel,
                t.CreatedAtUtc,
                t.Observacao,
                Nome = t.RegistradoPorUsuario != null ? t.RegistradoPorUsuario.Nome : null,
                TemArquivo = t.ArquivoTamanhoBytes != null,
                t.ArquivoNome,
            })
            .FirstOrDefaultAsync(ct);
        if (manual is not null)
            return new TermoCompromissoEpiDto(SituacaoTermoCompromissoEpi.Manual, manual.DataAssinaturaPapel, null,
                manual.Nome, manual.CreatedAtUtc, manual.Observacao, manual.TemArquivo, manual.ArquivoNome);

        return new TermoCompromissoEpiDto(SituacaoTermoCompromissoEpi.Pendente, null, null, null, null, null, false, null);
    }
}
