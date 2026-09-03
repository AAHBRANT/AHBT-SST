using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Pcmsos;

// Pcmso combina os campos genéricos de documento (nome/versão/validade/status) com os específicos de
// PcmsoDetalhe (médico responsável, funções/riscos/exames contemplados) — ambos vivem direto na
// entidade PcmsoDetalhe desde a reformulação de 2026-09-03 (ver nota lá).
public class PcmsoDto
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Versao { get; set; }
    public DateTime? Validade { get; set; }
    public DateTime DataEmissao { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public Guid? ObraId { get; set; }
    public Guid? SetorId { get; set; }
    public string? Arquivo { get; set; }
    public StatusPcmsoDocumento Status { get; set; }

    public string? MedicoResponsavelNome { get; set; }
    public string? MedicoResponsavelCrm { get; set; }
    public string? FuncoesContempladas { get; set; }
    public string? RiscosConsiderados { get; set; }
    public string? ExamesPrevistos { get; set; }
    public string? Periodicidades { get; set; }
    public string? UnidadesObrasAbrangidas { get; set; }
}
