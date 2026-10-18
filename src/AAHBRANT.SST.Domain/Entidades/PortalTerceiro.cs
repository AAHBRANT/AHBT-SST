using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Portal de terceiros (18/10/2026): a empresa terceirizada cadastra as próprias pessoas nas vagas do
// contrato e envia ASO/certificados antes da liberação na obra. Quem acessa não tem conta Entra ID —
// o acesso é um link exclusivo do contrato, confirmado por código enviado ao e-mail do contato.
//
// Só os HASHES (SHA-256) do link, do código e da sessão ficam gravados: um vazamento do banco não
// entrega um acesso utilizável. Um contrato tem no máximo um acesso ativo; gerar um novo revoga o
// anterior (GerarAcessoPortalTerceiroCommand).
public class AcessoPortalTerceiro : AuditableEntity
{
    public Guid ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    // Destino do código de confirmação — normalmente Empresa.ContatoEmail no momento da geração.
    public string Email { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? RevogadoEmUtc { get; set; }

    // Código de 6 dígitos de uso único: expira em minutos e é invalidado após poucas tentativas.
    public string? CodigoHash { get; set; }
    public DateTime? CodigoExpiraEmUtc { get; set; }
    public DateTime? CodigoEnviadoEmUtc { get; set; }
    public int TentativasCodigo { get; set; }

    public string? SessaoHash { get; set; }
    public DateTime? SessaoExpiraEmUtc { get; set; }
    public DateTime? UltimoAcessoEmUtc { get; set; }

    // Termo de uso e tratamento de dados (LGPD) aceito pela empresa antes de cadastrar pessoas ou
    // enviar documentos. A versão permite exigir novo aceite quando o texto mudar.
    public DateTime? TermoAceitoEmUtc { get; set; }
    public string? TermoVersaoAceita { get; set; }

    public bool EstaAtivo(DateTime agoraUtc) => Ativo && RevogadoEmUtc is null && ExpiraEmUtc > agoraUtc;
}

// Documento enviado pela empresa terceirizada para uma pessoa do contrato. Os dados declarados
// (datas, resultado, curso) são o que a empresa informou; o técnico confere contra o arquivo e só na
// aprovação nasce o Aso/Treinamento que a CalculadoraLiberacaoTerceirizado enxerga.
//
// LGPD: do ASO só se pede tipo, datas e resultado (apto/inapto) — nunca exames, CID ou observações
// clínicas. O arquivo fica guardado aqui para a conferência e como evidência do que foi recebido.
public class DocumentoEnviadoTerceiro : AuditableEntity
{
    public Guid AcessoPortalTerceiroId { get; set; }
    public AcessoPortalTerceiro? AcessoPortalTerceiro { get; set; }

    public Guid ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public TipoDocumentoTerceiro Tipo { get; set; }
    public StatusDocumentoTerceiro Status { get; set; } = StatusDocumentoTerceiro.AguardandoValidacao;

    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public long TamanhoBytes { get; set; }
    public string HashSha256 { get; set; } = string.Empty;

    // Data do exame (ASO) ou da realização (certificado).
    public DateTime DataDocumento { get; set; }
    public DateTime DataValidade { get; set; }

    public TipoExameAso? TipoExameAso { get; set; }
    public ResultadoAso? ResultadoAso { get; set; }

    public Guid? CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }
    public int? CargaHoraria { get; set; }
    public string? InstituicaoInstrutor { get; set; }
    public string? NumeroCertificado { get; set; }

    public string? MotivoRecusa { get; set; }
    public Guid? ValidadoPorUsuarioId { get; set; }
    public DateTime? ValidadoEmUtc { get; set; }

    // Registro gerado na aprovação — rastreia de qual envio veio cada Aso/Treinamento.
    public Guid? AsoGeradoId { get; set; }
    public Guid? TreinamentoGeradoId { get; set; }
}
