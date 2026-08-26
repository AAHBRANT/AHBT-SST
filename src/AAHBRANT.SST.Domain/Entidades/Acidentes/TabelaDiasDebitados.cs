using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Regra de Dias Debitados da NBR 14280 (Anexo/Quadro III). Só os dois casos com valor fixo e
// amplamente documentado são calculados automaticamente (Óbito e Incapacidade Permanente Total =
// 6.000 dias). Incapacidade Permanente Parcial depende da tabela detalhada de lesão × parte do
// corpo do Quadro III, que não é reproduzida aqui — fica como valor informado manualmente pelo
// usuário no registro do acidente. Decisão registrada em conversa de 2026-08-26: não fabricar os
// valores tabelados sem a fonte normativa oficial em mãos.
public static class TabelaDiasDebitados
{
    public const int DiasObitoOuIncapacidadeTotal = 6000;

    public static int Calcular(GravidadeAcidente gravidade, int? diasDebitadosInformados) => gravidade switch
    {
        GravidadeAcidente.Obito => DiasObitoOuIncapacidadeTotal,
        GravidadeAcidente.IncapacidadePermanenteTotal => DiasObitoOuIncapacidadeTotal,
        GravidadeAcidente.IncapacidadePermanenteParcial => diasDebitadosInformados ?? 0,
        _ => 0,
    };
}
