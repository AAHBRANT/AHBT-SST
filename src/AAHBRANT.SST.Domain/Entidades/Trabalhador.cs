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
    public string? Rg { get; set; }

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

    // Ficha de EPI reformulada — texto livre (o modelo oficial não define uma lista fechada de
    // turnos, então nenhuma lista fixa é assumida).
    public string? Turno { get; set; }

    // Integração com Telegram (DDS Fase 3): ChatId só é preenchido depois que o trabalhador
    // manda /start <codigo> para o bot — bots não podem iniciar a conversa. CodigoVinculo é o
    // código temporário exibido no perfil para o trabalhador usar nesse /start.
    public long? TelegramChatId { get; set; }
    public string? TelegramCodigoVinculo { get; set; }
    public DateTime? TelegramVinculadoEm { get; set; }

    // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §2/§3) — PIN é o método de
    // reserva (crachá/QR + PIN) quando o leitor biométrico da obra falha ou está indisponível.
    // PinHash é calculado explicitamente pelo handler que define/troca o PIN (via
    // Infrastructure.Seguranca.PinHasher.GerarHash) — ao contrário de CpfHash, não existe um campo
    // de PIN em texto plano nesta entidade para recalcular automaticamente em AplicarAuditoria: o
    // PIN nunca deve transitar por uma propriedade rastreada pelo EF Core, mesmo que efêmera.
    public string? PinHash { get; set; }

    // Validade jurídica e LGPD (docs/Motor-Assinatura-Eletronica.md §4) — dois consentimentos
    // distintos e obrigatórios antes do trabalhador poder assinar por este motor:
    // TermoAceiteAssinaturaEletronicaEm = aceite geral do método eletrônico (MP 2.200-2/2001,
    // Art. 10 §2º); ConsentimentoBiometriaEm = consentimento específico para dado biométrico
    // sensível (LGPD art. 5º II, art. 11), só preenchido se a obra usa leitor biométrico.
    public DateTime? TermoAceiteAssinaturaEletronicaEm { get; set; }
    public DateTime? ConsentimentoBiometriaEm { get; set; }

    public ICollection<Aso> Asos { get; set; } = new List<Aso>();
    public ICollection<Treinamento> Treinamentos { get; set; } = new List<Treinamento>();
    public ICollection<EntregaEpi> EntregasEpi { get; set; } = new List<EntregaEpi>();
    public ICollection<RiscoTrabalhadorExposto> RiscosExpostos { get; set; } = new List<RiscoTrabalhadorExposto>();
    public ICollection<ExameComplementar> ExamesComplementares { get; set; } = new List<ExameComplementar>();
    public ICollection<AptidaoAtividadeEspecifica> AptidoesAtividadeEspecifica { get; set; } = new List<AptidaoAtividadeEspecifica>();
}
