using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Asos;

public class AsoDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public TipoExameAso Tipo { get; set; }
    public DateTime DataExame { get; set; }
    public DateTime DataValidade { get; set; }
    public ResultadoAso ResultadoStatus { get; set; }
    public string? ObservacoesClinicas { get; set; }
    public string? MedicoNome { get; set; }
    public string? MedicoCrm { get; set; }
}
