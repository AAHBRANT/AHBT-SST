using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Controle de Higienização — módulo pedido pelo usuário em 2026-08-24 (fora do MVP da §47, sem
// seção literal da Base de Conhecimento — proposta própria): cadastro de locais (ex. banheiro,
// refeitório) com periodicidade de limpeza em dias. O vencimento (próxima higienização devida) é
// calculado sob demanda a partir do último RegistroHigienizacao — não é um campo persistido, mesmo
// espírito de AsoValidoRule para não ficar dessincronizado do histórico real.
public class ItemHigienizacao : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Local { get; set; }
    public int PeriodicidadeDias { get; set; }

    public ICollection<RegistroHigienizacao> Registros { get; set; } = new List<RegistroHigienizacao>();
}

// Um registro por higienização executada. Evidência fotográfica obrigatória (pedido explícito do
// usuário) — mesmo padrão de DdsParticipante: binário no próprio banco, sem storage externo.
public class RegistroHigienizacao : AuditableEntity
{
    public Guid ItemHigienizacaoId { get; set; }
    public ItemHigienizacao? ItemHigienizacao { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public DateTime DataHora { get; set; }
    public string? Observacoes { get; set; }

    public byte[] FotoConteudo { get; set; } = Array.Empty<byte>();
    public string FotoContentType { get; set; } = string.Empty;
}
