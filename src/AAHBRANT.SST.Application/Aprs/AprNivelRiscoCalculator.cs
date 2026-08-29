using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Aprs;

// Formulário "APR – ANÁLISE PRELIMINAR DE RISCO | REV.02" (planilha do usuário, 2026-08-29), aba
// "Matriz de Risco": fórmula literal do documento — =IF(OR(P="",S=""),"",IF(P*S<=4,"BAIXO",
// IF(P*S<=9,"MODERADO",IF(P*S<=15,"ALTO","CRÍTICO")))). Calculadora pura (sem consulta ao banco),
// porque esta matriz é fixa no formulário, diferente da matriz configurável do módulo Riscos
// (MatrizRiscoConfig/NivelRiscoLookup) — ver disclosure em NivelRiscoApr (Enums.cs). Usada tanto
// para o risco inicial (P/S) quanto para o residual (P Res./S Res.) de cada AprEtapaRisco.
public static class AprNivelRiscoCalculator
{
    public static NivelRiscoApr Calcular(int probabilidade, int severidade)
    {
        var produto = probabilidade * severidade;
        if (produto <= 4) return NivelRiscoApr.Baixo;
        if (produto <= 9) return NivelRiscoApr.Moderado;
        if (produto <= 15) return NivelRiscoApr.Alto;
        return NivelRiscoApr.Critico;
    }
}
