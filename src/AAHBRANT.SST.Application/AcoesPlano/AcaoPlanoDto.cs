using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.AcoesPlano;

public class AcaoPlanoDto
{
    public Guid Id { get; set; }
    public string OrigemTipo { get; set; } = string.Empty;
    public Guid OrigemId { get; set; }
    public TipoAcaoPlano Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Fundamentacao { get; set; }
    public Guid? ResponsavelUsuarioId { get; set; }
    public string? ResponsavelUsuarioNome { get; set; }
    public PrioridadeAcao Prioridade { get; set; }
    public DateTime? Prazo { get; set; }
    public StatusControleRisco Status { get; set; }
    public DateTime? DataConclusao { get; set; }
    public DateTime? DataValidacao { get; set; }
    public Guid? ValidadoPorUsuarioId { get; set; }
    public string? ValidadoPorUsuarioNome { get; set; }

    public string? ConcluidoPorUsuarioNome { get; set; }
    public string? DescricaoConclusao { get; set; }

    // Preenchidos só no detalhe da ocorrência: ação que o sistema conclui sozinho ("Dds"/"Reuniao")
    // e o texto de como ("Ao encerrar o DDS de 24/10 com este tema"). Nulos = ação manual.
    public string? TipoResolucaoAutomatica { get; set; }
    public string? ResolucaoAutomatica { get; set; }

    // Vínculo de conclusão automática (treinamento/inspeção/EPI) e o andamento ("1 de 2 envolvidos
    // treinados"), também só no detalhe da ocorrência.
    public TipoResolucaoAcaoPlano? TipoResolucao { get; set; }
    public Guid? ResolucaoCursoTreinamentoId { get; set; }
    public TipoInspecao? ResolucaoTipoInspecao { get; set; }
    public Guid? ResolucaoTemaInspecaoId { get; set; }
    public Guid? ResolucaoCatalogoEpiId { get; set; }
    public string? ProgressoResolucao { get; set; }
    public string? RotuloAguardando { get; set; }
}
