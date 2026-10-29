using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Tabela oficial da CBO 2002 (Ministério do Trabalho, arquivo "Estrutura CBO", 29/10/2026): uma linha por título.
// Oficial = título da ocupação; os demais são sinônimos do mesmo código. É a fonte única do CBO das funções no PGR:
// o RH não grava mais CBO (decisão do usuário, 29/10/2026).
public class TituloCbo : AuditableEntity
{
    // Seis dígitos, sem traço (ex.: "717020"). Para exibir, CodigoCbo.Formatar devolve "7170-20".
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public bool Oficial { get; set; }
}
