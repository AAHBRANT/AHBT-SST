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

    // Regra única do ASO vigente (ver AsoVigente): o do exame mais recente da pessoa. Os demais são superados e
    // não podem aparecer como vencidos nem bloquear. Preenchido por AsoVigente.Marcar.
    public bool Vigente { get; set; }
    public DateTime? SubstituidoPorDataExame { get; set; }
}
