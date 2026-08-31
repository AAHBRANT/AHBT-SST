using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.PermissoesTrabalho;

public class PermissaoTrabalhoDto
{
    public Guid Id { get; set; }
    public string? NumeroPt { get; set; }
    public Guid AtividadeId { get; set; }
    public string AtividadeNome { get; set; } = string.Empty;
    public Guid? ObraId { get; set; }
    public string? ObraNome { get; set; }
    public string DescricaoAtividade { get; set; } = string.Empty;
    public string Local { get; set; } = string.Empty;
    public string? EmpresaExecutante { get; set; }
    public Guid? EquipeId { get; set; }
    public string? EquipeNome { get; set; }
    public DateTime Data { get; set; }
    public TimeSpan? HorarioInicio { get; set; }
    public TimeSpan? HorarioFim { get; set; }
    public DateTime? Validade { get; set; }
    public Guid? ResponsavelExecucaoUsuarioId { get; set; }
    public string? ResponsavelExecucaoUsuarioNome { get; set; }
    public Guid? ResponsavelAreaUsuarioId { get; set; }
    public string? ResponsavelAreaUsuarioNome { get; set; }
    public StatusPt Status { get; set; }
    public Guid? AutorizadoPorUsuarioId { get; set; }
    public string? AutorizadoPorUsuarioNome { get; set; }
    public DateTime? DataAutorizacao { get; set; }
    public DateTime? DataAssinaturaExecucao { get; set; }
    public Guid? ResponsavelSstUsuarioId { get; set; }
    public string? ResponsavelSstUsuarioNome { get; set; }
    public DateTime? DataAssinaturaSst { get; set; }
    public Guid? SuspensaPorUsuarioId { get; set; }
    public string? SuspensaPorUsuarioNome { get; set; }
    public DateTime? DataSuspensao { get; set; }
    public string? MotivoSuspensao { get; set; }
    public Guid? RevalidadaPorUsuarioId { get; set; }
    public string? RevalidadaPorUsuarioNome { get; set; }
    public DateTime? DataRevalidacao { get; set; }
    public Guid? EncerradaPorUsuarioId { get; set; }
    public string? EncerradaPorUsuarioNome { get; set; }
    public DateTime? DataEncerramento { get; set; }
    public string? ObservacoesEncerramento { get; set; }
    public string? OutrosEpis { get; set; }
    public string? OutrosEpcs { get; set; }
}

public class PermissaoTrabalhoPreRequisitoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public ItemPreRequisitoPt Item { get; set; }
    public bool Atendido { get; set; }
}

public class PermissaoTrabalhoTipoTrabalhoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public TipoTrabalhoEspecialPt Tipo { get; set; }
    public string? DescricaoOutro { get; set; }
}

public class PermissaoTrabalhoVerificacaoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public ItemVerificacaoPt Item { get; set; }
    public RespostaVerificacaoPt? Resposta { get; set; }
}

public class PermissaoTrabalhoEpiDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public ItemEpiPt Item { get; set; }
    public string? Complemento { get; set; }
}

public class PermissaoTrabalhoEpcDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public ItemEpcPt Item { get; set; }
}

public class PermissaoTrabalhoRiscoCriticoDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public string RiscoCondicao { get; set; } = string.Empty;
    public string? ControleComplementar { get; set; }
    public string? ResponsavelEvidencia { get; set; }
}

public class PermissaoTrabalhoResponsavelDto
{
    public Guid Id { get; set; }
    public Guid PermissaoTrabalhoId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public string TrabalhadorNome { get; set; } = string.Empty;
    public string? TrabalhadorFuncaoNome { get; set; }
}

// Composição por query, não por tabela nova — mesmo princípio já usado em AprDetalheDto.
public class PermissaoTrabalhoDetalheDto
{
    public PermissaoTrabalhoDto PermissaoTrabalho { get; set; } = null!;
    public List<PermissaoTrabalhoPreRequisitoDto> PreRequisitos { get; set; } = new();
    public List<PermissaoTrabalhoTipoTrabalhoDto> TiposTrabalho { get; set; } = new();
    public List<PermissaoTrabalhoVerificacaoDto> Verificacoes { get; set; } = new();
    public List<PermissaoTrabalhoEpiDto> Epis { get; set; } = new();
    public List<PermissaoTrabalhoEpcDto> Epcs { get; set; } = new();
    public List<PermissaoTrabalhoRiscoCriticoDto> RiscosCriticos { get; set; } = new();
    public List<PermissaoTrabalhoResponsavelDto> Responsaveis { get; set; } = new();
}
