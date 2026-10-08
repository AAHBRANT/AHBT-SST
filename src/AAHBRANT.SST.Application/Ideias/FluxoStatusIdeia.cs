using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Ideias;

// Especificação §6 — transições permitidas e a permissão exigida para cada destino (§17).
public static class FluxoStatusIdeia
{
    private static readonly Dictionary<StatusIdeia, StatusIdeia[]> Permitidas = new()
    {
        [StatusIdeia.NovaIdeia] = [StatusIdeia.EmAnalise, StatusIdeia.Adiada, StatusIdeia.Descartada],
        [StatusIdeia.EmAnalise] = [StatusIdeia.AguardandoDecisao, StatusIdeia.Adiada, StatusIdeia.Descartada],
        [StatusIdeia.AguardandoDecisao] = [StatusIdeia.Aprovada, StatusIdeia.EmAnalise, StatusIdeia.Adiada, StatusIdeia.Descartada],
        [StatusIdeia.Aprovada] = [StatusIdeia.Priorizada, StatusIdeia.Adiada, StatusIdeia.Descartada],
        [StatusIdeia.Priorizada] = [StatusIdeia.EmDesenvolvimento, StatusIdeia.Adiada],
        [StatusIdeia.EmDesenvolvimento] = [StatusIdeia.EmTesteValidacao],
        [StatusIdeia.EmTesteValidacao] = [StatusIdeia.Implantada, StatusIdeia.EmDesenvolvimento],
        [StatusIdeia.Implantada] = [],
        [StatusIdeia.Adiada] = [StatusIdeia.EmAnalise, StatusIdeia.AguardandoDecisao],
        [StatusIdeia.Descartada] = [StatusIdeia.EmAnalise],
    };

    public static bool Permite(StatusIdeia de, StatusIdeia para) => Permitidas[de].Contains(para);

    public static IReadOnlyList<StatusIdeia> Destinos(StatusIdeia de) => Permitidas[de];

    // Permissão RBAC exigida para levar a ideia a um status.
    public static string PermissaoExigida(StatusIdeia destino) => destino switch
    {
        StatusIdeia.EmAnalise or StatusIdeia.AguardandoDecisao => "ideia:analisar",
        StatusIdeia.Aprovada or StatusIdeia.Priorizada or StatusIdeia.Adiada or StatusIdeia.Descartada => "ideia:decidir",
        _ => "ideia:desenvolver",
    };

    public static bool ExigeJustificativa(StatusIdeia destino)
        => destino is StatusIdeia.Adiada or StatusIdeia.Descartada;
}
