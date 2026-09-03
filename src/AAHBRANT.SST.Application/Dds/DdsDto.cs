using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Dds;

public class DdsTemaAtividadeDto
{
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string? PerigoNome { get; set; }
    public string? PerigoDescricao { get; set; }
    public string? Consequencia { get; set; }
    public string? ControlesExistentes { get; set; }
    public string? ControlesAdicionais { get; set; }
}

public class DdsDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid? DdsSemanalId { get; set; }
    public DateTime Data { get; set; }
    public Guid ResponsavelUsuarioId { get; set; }
    public string ResponsavelUsuarioNome { get; set; } = string.Empty;
    public Guid? CatalogoTemaDdsId { get; set; }
    public string? TemaLivreNome { get; set; }
    public string? TemaLivreDescricao { get; set; }
    public StatusDds Status { get; set; }
    public List<DdsTemaAtividadeDto> TemasAtividades { get; set; } = new();
    // Conveniência derivada de TemasAtividades (mesmo dado, só os nomes) — usada onde só o nome
    // da atividade importa (ex.: DdsDetalhePage.tsx), sem repetir o objeto inteiro.
    public List<string> AtividadesNomes { get; set; } = new();
    public int TotalItensChecklist { get; set; }
    public int ItensVerificados { get; set; }
    public int TotalParticipantes { get; set; }
    public int TotalFotosEvidencia { get; set; }
    public bool SemExpediente { get; set; }
    public string? MotivoSemExpediente { get; set; }
}

public class DdsItemChecklistDto
{
    public Guid Id { get; set; }
    public Guid DdsId { get; set; }
    public Guid? RiscoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Verificado { get; set; }
}

public class DdsParticipanteDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public TipoFotoParticipante FotoTipo { get; set; }
    public double? ScoreConfianca { get; set; }
    public DateTime? TelegramEnviadoEm { get; set; }
    public DateTime? TelegramConfirmadoEm { get; set; }
}

public class DdsFotoEvidenciaDto
{
    public Guid Id { get; set; }
    public int Ordem { get; set; }
}

// Composição por query (mesmo padrão de InspecaoDetalheDto) — não é uma tabela nova.
public class DdsDetalheDto
{
    public DdsDto Dds { get; set; } = null!;
    public List<DdsItemChecklistDto> ItensChecklist { get; set; } = new();
    public List<DdsParticipanteDto> Participantes { get; set; } = new();
    public List<DdsFotoEvidenciaDto> FotosEvidencia { get; set; } = new();
}
