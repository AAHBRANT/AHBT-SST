using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Solicitação de reposição de EPI (especificação 21/10/2026, Módulo 2): retrato do que está abaixo do
// mínimo na obra no momento em que o técnico gera o documento, assinado por ele com um clique. Serve de
// pedido interno para Suprimentos enquanto o envio automático ao GSUPRI não existir. Os itens guardam
// nome, CA e números da hora — o documento não muda se o estoque mudar depois.
public class SolicitacaoReposicaoEpi : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public Guid? SolicitanteUsuarioId { get; set; }
    public Usuario? SolicitanteUsuario { get; set; }
    public string SolicitanteNome { get; set; } = string.Empty;
    public int PrazoReposicaoDias { get; set; }
    public string? Observacao { get; set; }

    public ICollection<SolicitacaoReposicaoEpiItem> Itens { get; set; } = new List<SolicitacaoReposicaoEpiItem>();
}

public class SolicitacaoReposicaoEpiItem : AuditableEntity
{
    public Guid SolicitacaoReposicaoEpiId { get; set; }
    public SolicitacaoReposicaoEpi? SolicitacaoReposicaoEpi { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public string EpiNome { get; set; } = string.Empty;
    public string? CertificadoAprovacaoNumero { get; set; }
    public int Saldo { get; set; }
    public int Reservado { get; set; }
    public int EstoqueMinimo { get; set; }
    public int QuantidadeSugerida { get; set; }
    public string? TamanhosEmFalta { get; set; }
}
