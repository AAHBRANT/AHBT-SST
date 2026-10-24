using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Veículo/equipamento pesado cadastrado por obra para inspeção de segurança (checklist por tipo).
// Cadastro 100% manual no SST, sem integração com outro sistema (diferente de Alojamento, que vem
// do G-RH). Dados de frota (RENAVAM, chassi, licenciamento, seguro) ficam fora: são do setor de
// Frota. Mudar de obra é permitido — o histórico de inspeções acompanha o veículo (VeiculoId).
public class Veiculo : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public TipoVeiculo Tipo { get; set; }

    // "Placa/Prefixo" da planilha de checklist (ex.: "ABC1D23" ou "JOHN DEERE 310 P").
    public string PlacaPrefixo { get; set; } = string.Empty;
    public string? MarcaModelo { get; set; }
    public int? Ano { get; set; }
    public string? Cor { get; set; }

    // Subcontratada/locadora dona do veículo (ex.: "AAHBRANT", "PATROLL LOCAÇÕES").
    public string? Empresa { get; set; }
    public string? Responsavel { get; set; }
}
