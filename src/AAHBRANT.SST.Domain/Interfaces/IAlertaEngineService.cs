using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Interfaces;

// Motor Central de Alertas (requisito do usuário, 2026-08-24) — generaliza o padrão de
// vencimento/alerta para qualquer módulo, em vez de cada um reimplementar sua própria checagem de
// dias/severidade. Espelha o Strategy pattern já validado em IEligibilityService/IEligibilityRule:
// uma implementação de IAlertaOrigemProvider por módulo (Aso, Treinamento hoje; Extintor/
// Equipamento/Epi/Documento em fases seguintes), avaliadas em conjunto pelo
// IAlertaEngineService contra as RegraAlerta configuráveis daquele módulo.
public interface IAlertaOrigemProvider
{
    TipoModuloAlerta Modulo { get; }

    Task<List<AlertaOrigemItem>> ObterItensAsync(CancellationToken ct = default);
}

// Um item rastreável com data de vencimento/próxima ação (ASO, treinamento, item de higienização
// etc.), na forma genérica que o motor precisa para decidir severidade e (des)duplicar o Alerta.
public class AlertaOrigemItem
{
    public string EntidadeOrigemTipo { get; set; } = string.Empty;
    public Guid EntidadeOrigemId { get; set; }
    public DateTime DataVencimento { get; set; }

    // TipoAlertaVencido é opcional porque nem todo módulo tem um valor "Vencido" próprio no enum.
    // Quando nulo, o motor usa TipoAlertaVencendo também para o caso vencido, diferenciando só
    // pelo texto.
    public TipoAlerta TipoAlertaVencendo { get; set; }
    public TipoAlerta? TipoAlertaVencido { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public Guid? TrabalhadorId { get; set; }
    public Guid? ObraId { get; set; }
}

public interface IAlertaEngineService
{
    // Roda todos os IAlertaOrigemProvider registrados, compara cada item contra as RegraAlerta do
    // módulo dele e cria/atualiza/resolve os Alerta correspondentes. Chamado periodicamente pelo
    // AAHBRANT.SST.Worker (BackgroundService) — ver AlertaEngineWorker.
    Task ProcessarAsync(CancellationToken ct = default);
}
