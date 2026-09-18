using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class SuporteIaSolicitacao : AuditableEntity
{
    public TipoSolicitacaoSuporteIa Tipo { get; set; }
    public SeveridadeSolicitacaoSuporteIa SeveridadeInformada { get; set; } = SeveridadeSolicitacaoSuporteIa.Media;
    public StatusSolicitacaoSuporteIa Status { get; set; } = StatusSolicitacaoSuporteIa.Recebida;

    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public string? UrlContexto { get; set; }

    public Guid? SolicitanteUsuarioId { get; set; }
    public Usuario? SolicitanteUsuario { get; set; }
    public string? SolicitanteNome { get; set; }
    public string? SolicitanteEmail { get; set; }

    public ResultadoTriagemSuporteIa ResultadoTriagem { get; set; } = ResultadoTriagemSuporteIa.RespostaAoUsuario;
    public bool RequerAlteracaoCodigo { get; set; }
    public string RespostaAoUsuario { get; set; } = string.Empty;
    public string DemandaReduzida { get; set; } = string.Empty;
    public string SolucaoProposta { get; set; } = string.Empty;
    public string? EvidenciasTecnicas { get; set; }

    public DateTime TriadoEmUtc { get; set; }
    public DateTime? EncaminhadoEmUtc { get; set; }

    public Guid? AlertaId { get; set; }
    public Alerta? Alerta { get; set; }
}
