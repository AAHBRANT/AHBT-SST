using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public enum TipoRelatorio
{
    // Lista de presença de um DDS (diária, 08:00): quem estava, hora da assinatura, ausentes e duração.
    ListaPresencaDds = 1,
    // Boletim semanal por obra e consolidado (segunda, 07:00).
    BoletimSemanal = 2,
    // Aviso de ocorrência registrada (na hora).
    Ocorrencia = 3,
}

public enum CanalEnvioRelatorio
{
    Telegram = 1,
    Sininho = 2,
}

// Cada relatório gerado pelo sistema, com a imagem (PNG) e o PDF de detalhe quando houver. Só matrícula,
// nome de quem assinou o DDS e quantidades: nunca dado de saúde, lesão ou foto de pessoa. A ChaveUnica
// garante que o mesmo DDS ou a mesma semana de uma obra sai uma única vez, mesmo se o Worker reiniciar.
public class RelatorioGerado : AuditableEntity
{
    public TipoRelatorio Tipo { get; set; }

    // Obra do relatório; nulo no consolidado (todas as obras).
    public Guid? ObraId { get; set; }

    // Ex.: "dds:{ddsId}" ou "boletim:{obraId}:{inicioDaSemana:yyyyMMdd}".
    public string ChaveUnica { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    // Texto curto (legenda do Telegram e texto do sininho).
    public string Resumo { get; set; } = string.Empty;

    public byte[] Imagem { get; set; } = Array.Empty<byte>();
    public byte[]? Pdf { get; set; }
    public string? PdfNome { get; set; }

    // Registro de origem (por exemplo, o Id do DDS) e o período que o relatório cobre.
    public Guid? ReferenciaId { get; set; }
    public DateTime? PeriodoInicio { get; set; }
    public DateTime? PeriodoFim { get; set; }

    public DateTime GeradoEm { get; set; }

    public ICollection<RelatorioEnvio> Envios { get; set; } = new List<RelatorioEnvio>();
}

// Resultado de cada tentativa de entrega (um por canal e destinatário): alimenta o aviso técnico quando
// algo não chega.
public class RelatorioEnvio : AuditableEntity
{
    public Guid RelatorioGeradoId { get; set; }
    public RelatorioGerado? RelatorioGerado { get; set; }

    public CanalEnvioRelatorio Canal { get; set; }
    public Guid? UsuarioId { get; set; }
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
}

// Quem recebe quais relatórios de quais obras. ObraId nulo = todas as obras (diretoria, consolidado).
public class DestinatarioRelatorio : AuditableEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid? ObraId { get; set; }
    public Obra? Obra { get; set; }

    public bool ListaPresenca { get; set; }
    public bool BoletimSemanal { get; set; }
    public bool Ocorrencia { get; set; }
}

// Uma linha por tipo de relatório agendado e dia (data de Brasília): garante que o disparo das 08:00 roda
// uma vez por dia e permite recuperar o atraso se o Worker estiver fora do ar na hora.
public class ExecucaoRelatorioAgendado : AuditableEntity
{
    public TipoRelatorio Tipo { get; set; }
    public DateTime Data { get; set; }
    public DateTime ExecutadoEm { get; set; }
    public int RelatoriosGerados { get; set; }
}
