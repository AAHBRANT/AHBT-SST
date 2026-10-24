using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Rascunho do gerador de PGR a partir da EAP da obra (21/10/2026). Guarda o passo a passo do assistente
// (estrutura da EAP, GHEs, funções, riscos e medidas sugeridos) como JSON até o Engenheiro de Segurança
// aprovar; só na aprovação nascem o Pgr vigente, os GHEs, as atividades, os riscos e as medidas.
// A EAP vem por arquivo enviado; depois virá do G-COM (decisão do usuário).
public class GeracaoPgrEap : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public DateTime DataElaboracao { get; set; }
    public DateTime? DataProximaRevisao { get; set; }
    public Guid? ElaboradoPorUsuarioId { get; set; }
    public string? ElaboradoPorNome { get; set; }

    public string NomeArquivoEap { get; set; } = string.Empty;
    // Origem da EAP: "Arquivo" hoje; "G-COM" quando a integração existir.
    public string OrigemEap { get; set; } = "Arquivo";

    public StatusGeracaoPgrEap Status { get; set; } = StatusGeracaoPgrEap.Rascunho;
    // Passo em que o assistente parou (1 a 5).
    public int Passo { get; set; } = 2;
    public string EstruturaJson { get; set; } = "{}";

    public string? MotivoDevolucao { get; set; }
    public Guid? AprovadoPorUsuarioId { get; set; }
    public string? AprovadoPorNome { get; set; }
    public DateTime? AprovadoEmUtc { get; set; }
    public Guid? PgrId { get; set; }
    public Pgr? Pgr { get; set; }
}
