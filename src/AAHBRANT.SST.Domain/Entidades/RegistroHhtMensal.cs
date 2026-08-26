using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Lançamento mensal de Horas-Homem Trabalhadas (HHT) por obra — insumo do cálculo da Taxa de
// Gravidade (NBR 14280, ver TabelaDiasDebitados) exibida no Painel Inicial. Vocabulário/
// granularidade sem citação literal na Base de Conhecimento — mensal por obra foi a opção
// escolhida pelo usuário entre as alternativas apresentadas em 2026-08-26.
public class RegistroHhtMensal : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public int Ano { get; set; }
    public int Mes { get; set; }
    public int HorasHomemTrabalhadas { get; set; }
}
