using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class Obra : AuditableEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public StatusObra Status { get; set; } = StatusObra.Planejada;

    public DateTime? DataInicio { get; set; }
    public DateTime? DataPrevisaoTermino { get; set; }
    public DateTime? DataTerminoReal { get; set; }

    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    public ICollection<Setor> Setores { get; set; } = new List<Setor>();
    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
}
