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

    // Opcional desde a Integração G-RH (2026-09-09) — o G-RH não rastreia matrícula (vem sempre nula
    // na carga/nos eventos de colaborador), diferente da criação manual pela tela do SST, onde
    // continua obrigatória (ver CriarTrabalhadorCommandValidator). O índice único
    // (ObraId, Matricula) continua funcionando: SQL Server trata cada NULL como distinto.
    public string? Matricula { get; set; }
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

    // Campos sincronizados do G-RH (Integração G-RH, 2026-09-09) — G-RH é a fonte única de cadastro
    // para estes dados; o SST só reflete o que chega via SincronizarColaboradorGrhCommand, nunca
    // edita manualmente pela própria tela (ver disclosure no command). Pis não usa o mesmo
    // ValueConverter de criptografia do Cpf — simplificação deliberada desta primeira versão,
    // documentada como pendência LGPD a avaliar (mesma sensibilidade de dado pessoal do CPF).
    public string? Pis { get; set; }
    public string? Ctps { get; set; }
    public DateTime? DataNascimento { get; set; }
    public string? NomeMae { get; set; }
    public string? Endereco { get; set; }
    public string? Municipio { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
    public decimal? Salario { get; set; }
    public SituacaoTrabalhador Situacao { get; set; } = SituacaoTrabalhador.Ativo;
    public DateTime? DataFimExperiencia1 { get; set; }
    public DateTime? DataFimExperiencia2 { get; set; }
    public string? TamanhoBlusaEpi { get; set; }
    public string? TamanhoCalcaEpi { get; set; }
    public string? TamanhoCalcadoEpi { get; set; }

    // Ficha de EPI reformulada — texto livre (o modelo oficial não define uma lista fechada de
    // turnos, então nenhuma lista fixa é assumida).
    public string? Turno { get; set; }

    // Foto de perfil — mesmo padrão de Obra.LogoConteudo/LogoContentType (armazenada direto no SQL
    // Server, sem depender de um serviço de blob storage externo).
    public byte[]? FotoConteudo { get; set; }
    public string? FotoContentType { get; set; }

    // Integração com Telegram (DDS Fase 3): ChatId só é preenchido depois que o trabalhador
    // manda /start <codigo> para o bot — bots não podem iniciar a conversa. CodigoVinculo é o
    // código temporário exibido no perfil para o trabalhador usar nesse /start.
    public long? TelegramChatId { get; set; }
    public string? TelegramCodigoVinculo { get; set; }
    public DateTime? TelegramVinculadoEm { get; set; }

    // Validade jurídica e LGPD (docs/Motor-Assinatura-Eletronica.md §4) — dois consentimentos
    // distintos e obrigatórios antes do trabalhador poder assinar por este motor:
    // TermoAceiteAssinaturaEletronicaEm = aceite geral do método eletrônico (MP 2.200-2/2001,
    // Art. 10 §2º); ConsentimentoBiometriaEm = consentimento específico para dado biométrico
    // sensível (LGPD art. 5º II, art. 11), só preenchido se a obra usa leitor biométrico.
    public DateTime? TermoAceiteAssinaturaEletronicaEm { get; set; }
    public DateTime? ConsentimentoBiometriaEm { get; set; }

    // Id do Person no Azure Face API (PersonGroup da obra) — gerado no cadastro facial. Reaproveita
    // ConsentimentoBiometriaEm acima como consentimento LGPD (mesma categoria de dado biométrico
    // sensível — LGPD art. 5º II — não é um consentimento separado para "digital" vs. "facial").
    public string? AzureFacePersonId { get; set; }

    public ICollection<Aso> Asos { get; set; } = new List<Aso>();
    public ICollection<Treinamento> Treinamentos { get; set; } = new List<Treinamento>();
    public ICollection<EntregaEpi> EntregasEpi { get; set; } = new List<EntregaEpi>();
    public ICollection<EntregaUniforme> EntregasUniforme { get; set; } = new List<EntregaUniforme>();
    public ICollection<TrabalhadorTamanhoUniforme> TamanhosUniforme { get; set; } = new List<TrabalhadorTamanhoUniforme>();
    public ICollection<RiscoTrabalhadorExposto> RiscosExpostos { get; set; } = new List<RiscoTrabalhadorExposto>();
    public ICollection<ExameComplementar> ExamesComplementares { get; set; } = new List<ExameComplementar>();
    public ICollection<AptidaoAtividadeEspecifica> AptidoesAtividadeEspecifica { get; set; } = new List<AptidaoAtividadeEspecifica>();
}
