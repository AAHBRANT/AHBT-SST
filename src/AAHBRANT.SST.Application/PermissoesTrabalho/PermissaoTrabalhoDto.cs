using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.PermissoesTrabalho;

public class PermissaoTrabalhoDto
{
    public Guid Id { get; set; }
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public string Local { get; set; } = string.Empty;
    public Guid? EquipeId { get; set; }
    public string? EquipeNome { get; set; }
    public DateTime Data { get; set; }
    public TimeSpan? HorarioInicio { get; set; }
    public TimeSpan? HorarioFim { get; set; }
    public DateTime? Validade { get; set; }
    public StatusPt Status { get; set; }
    public Guid? AutorizadoPorUsuarioId { get; set; }
    public string? AutorizadoPorUsuarioNome { get; set; }
    public DateTime? DataAutorizacao { get; set; }
    public Guid? EncerradaPorUsuarioId { get; set; }
    public string? EncerradaPorUsuarioNome { get; set; }
    public DateTime? DataEncerramento { get; set; }
    public string? ObservacoesEncerramento { get; set; }
}

public class PermissaoTrabalhoPerigoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public Guid PerigoId { get; set; }
    public string PerigoNome { get; set; } = string.Empty;
}

public class PermissaoTrabalhoControleDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public class PermissaoTrabalhoRequisitoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Atendido { get; set; }
}

public class PermissaoTrabalhoResponsavelDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
}

// Composição por query, não por tabela nova — mesmo princípio já usado em AprDetalheDto.
public class PermissaoTrabalhoDetalheDto
{
    public PermissaoTrabalhoDto PermissaoTrabalho { get; set; } = null!;
    public List<PermissaoTrabalhoPerigoDto> Perigos { get; set; } = new();
    public List<PermissaoTrabalhoControleDto> Controles { get; set; } = new();
    public List<PermissaoTrabalhoRequisitoDto> Requisitos { get; set; } = new();
    public List<PermissaoTrabalhoResponsavelDto> Responsaveis { get; set; } = new();
}
