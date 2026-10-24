using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Obras;

public class ObraDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public StatusObra Status { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataPrevisaoTermino { get; set; }
    public DateTime? DataTerminoReal { get; set; }
    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cnpj { get; set; }
    public bool TemLogo { get; set; }

    // Flags de MetodoAutenticacaoObra: quais métodos de assinatura eletrônica a obra aceita.
    public MetodoAutenticacaoObra MetodosAssinatura { get; set; }
}
