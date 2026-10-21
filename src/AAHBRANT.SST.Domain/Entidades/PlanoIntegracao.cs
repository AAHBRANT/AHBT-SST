using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Plano de integração do funcionário (especificação de 21/10/2026, Módulo 1): nasce quando alguém
// entra na obra (admissão), troca de função ou é transferido de obra. Guarda só o "quando" e o
// "quem cuida" — os passos (ASO, biometria, treinamentos, kit de EPI) são calculados na hora a partir
// dos dados reais do trabalhador, para nunca ficarem desatualizados em relação ao resto do sistema.
public class PlanoIntegracao : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public MotivoPlanoIntegracao Motivo { get; set; } = MotivoPlanoIntegracao.Admissao;
    public StatusPlanoIntegracao Status { get; set; } = StatusPlanoIntegracao.Aberto;
    public DateTime? EncerradoEm { get; set; }

    // "Assumir" (vários técnicos na mesma obra): o primeiro que assume fica como responsável e os
    // demais veem quem está cuidando.
    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
    public DateTime? AssumidoEm { get; set; }
}

// Kit de EPI separado para um trabalhador. Não baixa o estoque: o item sai do disponível
// (saldo físico − reservas ativas) e a baixa definitiva acontece na entrega assinada
// (CriarEntregaEpiCommand marca a reserva como Entregue). Separado ou em falta não é gravado —
// é a posição da reserva na fila (ordem de criação) frente ao saldo físico, calculada na consulta.
public class ReservaEpi : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public int Quantidade { get; set; } = 1;
    // Tamanho informado pelo RH (blusa, calça ou calçado), quando o EPI é de um desses tipos.
    public string? Tamanho { get; set; }

    public StatusReservaEpi Status { get; set; } = StatusReservaEpi.Ativa;
    public Guid? EntregaEpiId { get; set; }
    public EntregaEpi? EntregaEpi { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public string? MotivoCancelamento { get; set; }
}
