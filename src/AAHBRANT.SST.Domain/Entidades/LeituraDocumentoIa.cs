using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public enum DocumentoLeituraIa
{
    Pgr = 1,
    Pcmso = 2,
}

public enum StatusLeituraIa
{
    Pendente = 1,
    Lendo = 2,
    Concluida = 3,
    Falhou = 4,
    Cadastrada = 5,
    Descartada = 6,
}

// "Ler com IA" (aprovado em 10/10/2026): leitura do PDF do PGR ou do PCMSO pela IA, em segundo plano.
// O resultado (ResultadoJson) fica guardado para a tela de revisão; nada entra na estrutura da obra até
// alguém clicar em "Cadastrar". PGR e PCMSO são lidos separadamente (decisão do usuário).
public class LeituraDocumentoIa : AuditableEntity
{
    public DocumentoLeituraIa Documento { get; set; }

    // Id do Pgr ou do PcmsoDetalhe lido; RevisaoId é a revisão cujo PDF foi lido (nulo se o PDF não
    // estava em nenhuma revisão).
    public Guid DocumentoId { get; set; }
    public Guid? RevisaoId { get; set; }
    public Guid ObraId { get; set; }

    public StatusLeituraIa Status { get; set; } = StatusLeituraIa.Pendente;
    public string? Etapa { get; set; }
    public int PassosConcluidos { get; set; }
    public int PassosTotal { get; set; }
    public string? ResultadoJson { get; set; }
    public string? Erro { get; set; }

    public DateTime? IniciadaEmUtc { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
    public DateTime? CadastradaEmUtc { get; set; }
}
