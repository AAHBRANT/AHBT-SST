using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Motor Central de Alertas + Cadastro de Ativos (requisito do usuário, 2026-08-25): entidade única
// para os ativos de SST monitorados pelo motor de alertas (extintores, equipamentos), discriminada
// por TipoAtivo — decisão de arquitetura confirmada pelo usuário, não escolha própria. Nome
// "AtivoSst" (não apenas "Ativo") para não colidir semanticamente com o campo booleano Ativo de
// soft-delete que toda AuditableEntity já tem. A validade aqui é um campo fixo armazenado
// (DataValidade) — não calculada a partir de um histórico de registros.
public class AtivoSst : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public TipoAtivo TipoAtivo { get; set; }

    public string Identificacao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Localizacao { get; set; }
    public DateTime DataValidade { get; set; }
    public string? Observacoes { get; set; }
}
