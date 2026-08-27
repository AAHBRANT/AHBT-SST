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

    // Ficha de EPI reformulada (docs/superpowers/specs/2026-08-27-ficha-epi-reformulada-design.md) —
    // CNPJ e logo da obra aparecem no cabeçalho da ficha consolidada; logo é opcional, com fallback
    // em texto (nome da obra) quando ausente.
    public string? Cnpj { get; set; }
    public byte[]? LogoConteudo { get; set; }
    public string? LogoContentType { get; set; }

    // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §2/§3) — cada obra decide
    // quais métodos aceita; ex.: obra sem leitor biométrico ainda comprado opera só com CrachaPin
    // até o hardware chegar. Default Nenhum: uma obra só passa a assinar depois de configurada
    // explicitamente, nunca por omissão.
    public MetodoAutenticacaoObra MetodosAutenticacaoHabilitados { get; set; } = MetodoAutenticacaoObra.Nenhum;

    public ICollection<Setor> Setores { get; set; } = new List<Setor>();
    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
}
