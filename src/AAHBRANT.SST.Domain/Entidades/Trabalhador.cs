using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

public class Trabalhador : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? SetorId { get; set; }
    public Setor? Setor { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;

    // LGPD: valor armazenado é sempre o CPF criptografado (AES-256-GCM via CpfCriptografiaConversor,
    // aplicado na configuração do EF Core). Nunca gravar/ler este campo fora do EF Core.
    public string Cpf { get; set; } = string.Empty;

    // Hash HMAC-SHA256 determinístico do CPF em texto plano, calculado automaticamente em
    // SstDbContext.AplicarAuditoria — existe só para permitir um índice único (Cpf criptografado
    // tem nonce aleatório, então nunca repete e não serviria para checar duplicidade).
    public string? CpfHash { get; set; }

    public TipoVinculo Vinculo { get; set; } = TipoVinculo.Clt;
    public DateTime DataAdmissao { get; set; }
    public DateTime? DataDemissao { get; set; }

    // Integração com Telegram (DDS Fase 3): ChatId só é preenchido depois que o trabalhador
    // manda /start <codigo> para o bot — bots não podem iniciar a conversa. CodigoVinculo é o
    // código temporário exibido no perfil para o trabalhador usar nesse /start.
    public long? TelegramChatId { get; set; }
    public string? TelegramCodigoVinculo { get; set; }
    public DateTime? TelegramVinculadoEm { get; set; }

    public ICollection<Aso> Asos { get; set; } = new List<Aso>();
    public ICollection<Treinamento> Treinamentos { get; set; } = new List<Treinamento>();
    public ICollection<EntregaEpi> EntregasEpi { get; set; } = new List<EntregaEpi>();
    public ICollection<RiscoTrabalhadorExposto> RiscosExpostos { get; set; } = new List<RiscoTrabalhadorExposto>();
}
