using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Pcmsos;

// Pcmso combina o DocumentoGestao genérico (nome/versão/validade/status/histórico de revisões,
// Tipo="PCMSO") com os campos específicos de PcmsoDetalhe (médico responsável, funções/riscos/exames
// contemplados) — ver PcmsoDetalhe para a decisão de reaproveitar o genérico em vez de duplicar campos.
public class PcmsoDto
{
    public Guid Id { get; set; }
    public Guid DocumentoGestaoId { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Versao { get; set; }
    public DateTime? Validade { get; set; }
    public DateTime DataEmissao { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public Guid? ObraId { get; set; }
    public Guid? SetorId { get; set; }
    public string? Arquivo { get; set; }
    // PENDENTE: era StatusDocumentoGestao — o enum foi removido junto com Gestão Documental
    // (2026-08-28); ver nota em PcmsoDetalhe (Domain/Entidades/SaudeOcupacional/SaudeOcupacional.cs).
    public int Status { get; set; }

    public string? MedicoResponsavelNome { get; set; }
    public string? MedicoResponsavelCrm { get; set; }
    public string? FuncoesContempladas { get; set; }
    public string? RiscosConsiderados { get; set; }
    public string? ExamesPrevistos { get; set; }
    public string? Periodicidades { get; set; }
    public string? UnidadesObrasAbrangidas { get; set; }
}
