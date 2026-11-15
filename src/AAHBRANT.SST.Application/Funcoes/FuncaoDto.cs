using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Funcoes;

public class FuncaoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CboCodigo { get; set; }
    public string? Descricao { get; set; }
    // Cor do capacete esperada para a função (0 = sem padrão; 1 a 6 = cores do padrão AAHBRANT).
    public CorCapacete CorCapacete { get; set; }
}
