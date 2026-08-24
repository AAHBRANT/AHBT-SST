using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Seção 27 da Base de Conhecimento (linhas 666-696) — registro de acidente/incidente/quase
// acidente/condição insegura/ato inseguro/doença ocupacional, com dados de trabalhador, obra,
// local, data/hora, atividade, descrição, lesão, consequência, atendimento, afastamento e CAT.
// Investigação (§28) modelada como metodologia + causas neste mesmo registro; ações do plano
// reutilizam a entidade genérica AcaoPlano (OrigemTipo = nameof(Acidente)).
public class Acidente : AuditableEntity
{
    public TipoOcorrencia Tipo { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid? AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public string Local { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public TimeSpan? Hora { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Lesao { get; set; }
    public string? Consequencia { get; set; }
    public string? Atendimento { get; set; }

    public bool HouveAfastamento { get; set; }
    public int? DiasAfastamento { get; set; }

    // Número/protocolo da Comunicação de Acidente de Trabalho (CAT), quando emitida.
    public string? NumeroCat { get; set; }

    public MetodologiaInvestigacao? MetodologiaInvestigacao { get; set; }
    public string? Causas { get; set; }

    public StatusAcidente Status { get; set; } = StatusAcidente.Registrado;
    public DateTime? DataConclusaoInvestigacao { get; set; }
}
