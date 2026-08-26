namespace AAHBRANT.SST.Application.RegistrosHhtMensais;

public class RegistroHhtMensalDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string? ObraNome { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public int HorasHomemTrabalhadas { get; set; }
}
