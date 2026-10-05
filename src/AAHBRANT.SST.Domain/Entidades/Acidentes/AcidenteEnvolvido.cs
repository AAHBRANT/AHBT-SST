using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Todos os funcionários envolvidos na ocorrência. Acidente.TrabalhadorId continua guardando o
// primeiro da lista (envolvido principal), usado pela integração com o G-RH, que recebe um só CPF.
public class AcidenteEnvolvido : AuditableEntity
{
    public Guid AcidenteId { get; set; }
    public Acidente? Acidente { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
}
