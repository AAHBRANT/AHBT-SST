using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Interfaces;

namespace AAHBRANT.SST.Application.Alertas.Motor;

// PENDENTE: cobria todo DocumentoGestao com Validade preenchida (inclusive PCMSO, que reaproveitava
// DocumentoGestao sem entidade de vencimento própria) — DocumentoGestao foi removido junto com
// Gestão Documental/Conformidade em 2026-08-28. Retorna lista vazia (sem gerar alertas de documento)
// em vez de lançar exceção, pois o AlertaEngineService não isola falha de um provider dos demais
// (Aso/Treinamento/Epi/etc. não podem parar de funcionar por causa deste).
public class DocumentoAlertaProvider : IAlertaOrigemProvider
{
    public TipoModuloAlerta Modulo => TipoModuloAlerta.Documento;

    public Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<AlertaOrigemItem>());
}
