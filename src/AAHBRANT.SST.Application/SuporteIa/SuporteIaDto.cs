using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.SuporteIa;

public class SuporteIaSolicitacaoDto
{
    public Guid Id { get; set; }
    public TipoSolicitacaoSuporteIa Tipo { get; set; }
    public SeveridadeSolicitacaoSuporteIa SeveridadeInformada { get; set; }
    public StatusSolicitacaoSuporteIa Status { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public string? UrlContexto { get; set; }
    public Guid? SolicitanteUsuarioId { get; set; }
    public string? SolicitanteNome { get; set; }
    public string? SolicitanteEmail { get; set; }
    public ResultadoTriagemSuporteIa ResultadoTriagem { get; set; }
    public bool RequerAlteracaoCodigo { get; set; }
    public string RespostaAoUsuario { get; set; } = string.Empty;
    public string DemandaReduzida { get; set; } = string.Empty;
    public string SolucaoProposta { get; set; } = string.Empty;
    public string? EvidenciasTecnicas { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime TriadoEmUtc { get; set; }
    public DateTime? EncaminhadoEmUtc { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelNome { get; set; }
    public DateTime? AprovadoEmUtc { get; set; }

    public string? NotaFechamento { get; set; }
    public DateTime? ConcluidoEmUtc { get; set; }

    public bool? ValidacaoConfirmada { get; set; }
    public string? ComentarioValidacao { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }

    // Calculado pelo controller a partir de quem está autenticado — decide no front se as ações de
    // validação (etapa 4) aparecem para esta pessoa, sem repetir a lógica de identidade do
    // solicitante no cliente.
    public bool SouSolicitante { get; set; }
}

public record TriagemSuporteIaResultado(
    ResultadoTriagemSuporteIa Resultado,
    bool RequerAlteracaoCodigo,
    string RespostaAoUsuario,
    string DemandaReduzida,
    string SolucaoProposta,
    string? EvidenciasTecnicas);
