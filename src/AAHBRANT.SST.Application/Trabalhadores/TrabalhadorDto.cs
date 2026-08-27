using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Trabalhadores;

public class TrabalhadorDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public Guid? SetorId { get; set; }
    public Guid? EquipeId { get; set; }
    public Guid FuncaoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public TipoVinculo Vinculo { get; set; }
    public DateTime DataAdmissao { get; set; }
    public DateTime? DataDemissao { get; set; }
    public string? Turno { get; set; }
    public bool TelegramVinculado { get; set; }
    public string? TelegramCodigoVinculo { get; set; }
}
