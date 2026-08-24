namespace AAHBRANT.SST.Domain.Interfaces;

// Motor de elegibilidade / bloqueio preventivo (Base de Conhecimento §45).
// Serviço central único, reutilizado por qualquer módulo (PT, autorização de altura,
// espaço confinado, elétrica) — nenhum módulo futuro deve reimplementar esta checagem.
public interface IEligibilityService
{
    Task<EligibilityResult> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default);
}

public class EligibilityRequest
{
    public Guid TrabalhadorId { get; set; }
    public Guid ObraId { get; set; }
    public Guid? AtividadeId { get; set; }
    public Guid? TipoAutorizacaoId { get; set; }
    public Guid? PermissaoTrabalhoId { get; set; }
    public string ContextoModulo { get; set; } = string.Empty;
}

public class EligibilityResult
{
    public bool Liberado { get; set; }
    public List<EligibilityCheckItem> Itens { get; set; } = new();
    public string? MotivoBloqueioResumo { get; set; }
}

public class EligibilityCheckItem
{
    public string Requisito { get; set; } = string.Empty;
    public bool Atendido { get; set; }
    public bool Critico { get; set; } = true;
    public string? Detalhe { get; set; }
}

// Uma implementação por tipo de requisito (Strategy pattern) — ex.: AsoValidoRule,
// TreinamentoValidoRule, AutorizacaoValidaRule. Registradas via DI e avaliadas em conjunto
// pelo IEligibilityService, configuradas por linhas de TipoAutorizacaoRequisito (Fase B).
public interface IEligibilityRule
{
    string NomeRequisito { get; }
    Task<EligibilityCheckItem> AvaliarAsync(EligibilityRequest request, CancellationToken ct = default);
}
