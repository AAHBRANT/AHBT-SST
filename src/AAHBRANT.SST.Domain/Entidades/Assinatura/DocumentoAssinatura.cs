using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Motor Central de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md) — genérico e
// reutilizável (EntidadeTipo/EntidadeId polimórfico, mesmo padrão de Evidencia/TrilhaAuditoria),
// entra primeiro no DDS e é pensado para Treinamento/EPI/APR/PT/Inspeções depois (§3/§5 do doc).
// Representa UM documento assinável (ex.: a lista de presença de um DDS específico); cada
// trabalhador que assina vira um DocumentoSignatario.
public class DocumentoAssinatura : AuditableEntity
{
    public string EntidadeTipo { get; set; } = string.Empty; // ex.: "Dds"
    public Guid EntidadeId { get; set; }

    public StatusDocumentoAssinatura Status { get; set; } = StatusDocumentoAssinatura.EmAndamento;

    // Preenchidos só na finalização (FinalizarDocumentoCommand — §5 do doc): hash do conteúdo final
    // (prova de integridade) e token da página pública de validação (/sst/validar/{token}). Nunca
    // expor Id/EntidadeId/dado pessoal na página pública — só o que TokenValidacaoPublica resolve.
    public string? ConteudoHash { get; set; }
    public string? TokenValidacaoPublica { get; set; }
    public DateTime? FinalizadoEm { get; set; }

    // Timestamp de quando o token de validação pública foi gerado pela primeira vez (Task 2 —
    // IRegistradorRastreabilidadeService), independente de o documento chegar a ser Finalizado. Usado
    // como "emitido em" na página pública/rodapé para documentos que nunca finalizam (CIPA, DDS
    // Semanal — nunca assinam digitalmente).
    public DateTime? RastreadoEm { get; set; }

    // PDF assinado gerado na finalização. Sem Blob Storage provisionado no projeto (confirmado em
    // diagnóstico anterior) — segue o mesmo padrão já usado por DdsParticipante.FotoConteudo
    // (varbinary(max) no próprio banco) em vez de introduzir uma dependência de storage nova.
    public byte[]? PdfConteudo { get; set; }

    // Integridade do CONTEÚDO, distinta de ConteudoHash (que cobre só a lista de signatários e não
    // detecta adulteração do documento em si). SHA-256 sobre os bytes exatos do PDF guardado em
    // PdfConteudo, calculado DEPOIS da geração — por isso não entra no rodapé do próprio PDF, o que
    // seria circular. É o que permite a terceiro conferir o arquivo em mãos contra o registro:
    // baixa o PDF pela página pública, calcula o SHA-256 e compara.
    //
    // Enquanto o documento aceita assinatura, arquivo e hash são substituídos a cada exportação (o
    // registro sempre reflete a última versão emitida). Depois de Finalizado, ficam congelados —
    // é a cópia que serve de prova.
    public string? HashPdf { get; set; }
    public DateTime? ArquivoAtualizadoEm { get; set; }

    public ICollection<DocumentoSignatario> Signatarios { get; set; } = new List<DocumentoSignatario>();
}

// Um signatário = um trabalhador que assinou este documento por um dos métodos habilitados na obra
// (Obra.MetodosAutenticacaoHabilitados). Não guarda dado biométrico bruto em nenhuma hipótese — o
// template de impressão digital nunca sai do leitor FIDO2 (§3 do doc); aqui só fica a prova de que a
// identificação aconteceu (método + quando), que é o que dá validade jurídica (MP 2.200-2/2001,
// Art. 10 §2º — ver §4 do doc) junto com o Termo de Aceite/Consentimento já registrados no perfil do
// trabalhador (Trabalhador.TermoAceiteAssinaturaEletronicaEm/ConsentimentoBiometriaEm).
public class DocumentoSignatario : AuditableEntity
{
    public Guid DocumentoAssinaturaId { get; set; }
    public DocumentoAssinatura? DocumentoAssinatura { get; set; }

    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public MetodoAutenticacaoAssinatura MetodoAutenticacao { get; set; }
    public DateTime AssinadoEm { get; set; }

    // Rastro de IP para o audit trail jurídico (Cofre de Assinaturas) — capturado no controller a
    // partir do HttpContext, nunca aceito do cliente (ver AssinaturaController.ObterIpCliente).
    public string? IpAddress { get; set; }

    // Evidência visual capturada na assinatura por reconhecimento facial. Fica apenas no cofre
    // interno/relatórios autenticados; a validação pública do documento não expõe esta imagem.
    public byte[]? FotoEvidenciaConteudo { get; set; }
    public string? FotoEvidenciaContentType { get; set; }
    public string? FotoEvidenciaHash { get; set; }
}
