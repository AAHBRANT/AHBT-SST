using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Tema de DDS agendado para um dia específico de uma obra (08/10/2026, pedido do usuário: toda
// ocorrência registrada por relato gera, obrigatoriamente, um tema para o DDS do dia seguinte).
// Quando o técnico abre o registro do DDS daquele dia, o tema já vem selecionado; quando o DDS é
// encerrado com ele, a ação de plano correspondente (AcaoPlanoId) é concluída sozinha.
public class TemaDdsAgendado : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    // Dia previsto (só a data) — o próximo dia útil depois da ocorrência.
    public DateTime Data { get; set; }

    public Guid CatalogoTemaDdsId { get; set; }
    public CatalogoTemaDds? CatalogoTemaDds { get; set; }

    // De onde veio o agendamento (hoje só Acidente), mesmo par polimórfico de AcaoPlano.
    public string OrigemTipo { get; set; } = string.Empty;
    public Guid OrigemId { get; set; }

    // Texto curto mostrado no DDS ("Sugerido a partir da ocorrência de 08/10 · Cabeceira norte da ponte").
    public string? DescricaoOrigem { get; set; }

    public Guid? AcaoPlanoId { get; set; }

    // Preenchido quando um DDS da obra é encerrado com este tema.
    public Guid? DdsId { get; set; }
    public DateTime? AplicadoEm { get; set; }
}
