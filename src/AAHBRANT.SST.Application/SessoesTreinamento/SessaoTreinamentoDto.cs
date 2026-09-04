using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.SessoesTreinamento;

public class SessaoTreinamentoDto
{
    public Guid Id { get; set; }
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public Guid CursoTreinamentoId { get; set; }
    public string CursoTreinamentoNome { get; set; } = string.Empty;
    public DateTime DataRealizacao { get; set; }
    public int CargaHorariaRealizada { get; set; }
    public string? InstituicaoInstrutor { get; set; }
    public string? NumeroCertificado { get; set; }
    public StatusSessaoTreinamento Status { get; set; }
    public DateTime? DataEncerramento { get; set; }
    public int TotalParticipantes { get; set; }
    public int TotalPresencasConfirmadas { get; set; }
    public int TotalFotosEvidencia { get; set; }
}

public class ParticipanteSessaoTreinamentoDto
{
    public Guid Id { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string? TrabalhadorMatricula { get; set; }
    public DateTime? PresencaConfirmadaEm { get; set; }
    public double? ScoreConfianca { get; set; }
    public Guid? TreinamentoGeradoId { get; set; }
}

public class FotoEvidenciaSessaoTreinamentoDto
{
    public Guid Id { get; set; }
    public int Ordem { get; set; }
}

// Composição por query (mesmo padrão de DdsDetalheDto) — não é uma tabela nova.
public class SessaoTreinamentoDetalheDto
{
    public SessaoTreinamentoDto Sessao { get; set; } = null!;
    public List<ParticipanteSessaoTreinamentoDto> Participantes { get; set; } = new();
    public List<FotoEvidenciaSessaoTreinamentoDto> FotosEvidencia { get; set; } = new();
}
