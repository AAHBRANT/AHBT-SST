using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

// Selo do adesivo de inspeção (17/11/2026). Texto gravado no JSON/PDF: Liberado, Inspecionado, NaoLiberado.
public enum SituacaoAdesivo
{
    Liberado = 1,
    Inspecionado = 2,
    NaoLiberado = 3,
}

// Regra pura (sem banco) do selo do adesivo:
//  - só inspeção Concluída (assinada) gera selo; qualquer outro status devolve null;
//  - qualquer item Não Conforme => NaoLiberado (uso bloqueado), seja qual for o modelo do objeto;
//  - senão o selo é o do modelo do objeto (Liberado para uso/operação, Inspecionado para local/dispositivo).
// Item sem resposta (null) e Não Aplicável não bloqueiam: uma inspeção Concluída já tem o checklist validado.
public static class ResultadoAdesivoInspecao
{
    public static SituacaoAdesivo? Calcular(
        StatusInspecao status,
        IEnumerable<StatusItemChecklist?> respostas,
        ModeloAdesivoInspecao modelo)
    {
        if (status != StatusInspecao.Concluida)
            return null;

        if (respostas.Any(r => r == StatusItemChecklist.NaoConforme))
            return SituacaoAdesivo.NaoLiberado;

        return modelo == ModeloAdesivoInspecao.Inspecionado
            ? SituacaoAdesivo.Inspecionado
            : SituacaoAdesivo.Liberado;
    }

    public static string Texto(SituacaoAdesivo situacao) => situacao switch
    {
        SituacaoAdesivo.Inspecionado => "Inspecionado",
        SituacaoAdesivo.NaoLiberado => "NaoLiberado",
        _ => "Liberado",
    };
}
