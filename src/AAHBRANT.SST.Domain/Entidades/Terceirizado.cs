using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — Empresa é
// cadastro enxuto (sem documentos obrigatórios na v1, só anexo livre via Evidencia). Pessoa
// terceirizada NÃO é uma entidade própria: continua sendo Trabalhador (Vinculo=Terceirizado), com
// EmpresaId/ContratoId novos (ver Task 2) — decisão do brainstorming para reaproveitar ASO/EPI/
// Treinamento sem duplicar nada.
public class Empresa : AuditableEntity
{
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string? TipoServicoPrestado { get; set; }
    public string? ContatoNome { get; set; }
    public string? ContatoTelefone { get; set; }
    public string? ContatoEmail { get; set; }
    public StatusEmpresa Status { get; set; } = StatusEmpresa.Ativa;

    public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    public ICollection<DocumentoEmpresaTerceirizada> Documentos { get; set; } = new List<DocumentoEmpresaTerceirizada>();
}

// Um contrato cobre sempre uma única Obra (decisão do brainstorming, simplifica porque
// EstoqueEpi/MatrizEpiFuncao já resolvem por Obra). Nasce pelo webhook do G-Juri
// (ContratoValidadoWebhookCommand) OU cadastrado à mão pelo técnico de segurança
// (CriarContratoCommand, 18/10/2026 — a integração do G-Juri ainda não existe e, sem contrato, não
// havia como cadastrar pessoa terceirizada).
public class Contrato : AuditableEntity
{
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public string NumeroContrato { get; set; } = string.Empty;
    public DateOnly DataInicioVigencia { get; set; }
    public DateOnly DataFimVigencia { get; set; }
    public StatusContrato Status { get; set; } = StatusContrato.Validado;
    public DateOnly? DataEncerramento { get; set; }

    // Chave de idempotência do webhook (Task 10/11) — único; um evento repetido do G-Juri nunca
    // duplica o Contrato. Nulo no contrato cadastrado à mão no SST.
    public string? GJuriContratoId { get; set; }

    public bool VeioDoGJuri => GJuriContratoId is not null;

    public ICollection<ContratoVagaFuncao> Vagas { get; set; } = new List<ContratoVagaFuncao>();
    public ICollection<Trabalhador> Trabalhadores { get; set; } = new List<Trabalhador>();

    // PDF do contrato enviado pelo técnico ("Subir contrato", especificação 21/10/2026). Tabela à parte,
    // mesmo padrão de ArquivoCertificadoTreinamento: as listagens nunca carregam os bytes.
    public ArquivoContrato? Arquivo { get; set; }
}

public class ArquivoContrato : AuditableEntity
{
    public Guid ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public long TamanhoBytes { get; set; }
}

// Documento que a AAHBRANT exige de toda empresa terceirizada (PGR, PCMSO, cartão CNPJ, contrato
// social...) — checklist da empresa, especificação 21/10/2026. Lista configurável pelo Técnico de
// Segurança; nasce com os quatro documentos acima (DocumentoExigidoEmpresaSeeder) e deve ser validada
// com o Jurídico. Desativar (Ativo=false) tira o documento do checklist sem apagar o que já foi enviado.
public class DocumentoExigidoEmpresa : AuditableEntity
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    // false = documento sem vencimento (ex.: contrato social); o checklist não pede data de validade.
    public bool ExigeValidade { get; set; } = true;
    public int Ordem { get; set; }
}

// Documento enviado pela empresa terceirizada para um item exigido. Um por empresa e documento:
// enviar de novo substitui (a versão nova é a que vale). Vencido, o item volta a ficar pendente no
// checklist e os técnicos das obras da empresa recebem alerta (DocumentoEmpresaTerceirizadaAlertaProvider).
public class DocumentoEmpresaTerceirizada : AuditableEntity
{
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public Guid DocumentoExigidoEmpresaId { get; set; }
    public DocumentoExigidoEmpresa? DocumentoExigido { get; set; }

    public DateOnly? DataValidade { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanhoBytes { get; set; }

    // Bytes em tabela à parte (mesmo motivo do ArquivoContrato): o checklist lista os documentos sem
    // carregar os arquivos.
    public ArquivoDocumentoEmpresaTerceirizada? Arquivo { get; set; }
}

public class ArquivoDocumentoEmpresaTerceirizada : AuditableEntity
{
    public Guid DocumentoEmpresaTerceirizadaId { get; set; }
    public DocumentoEmpresaTerceirizada? Documento { get; set; }
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}

// "5 pedreiros, 3 eletricistas" — o contrato chega do G-Juri só com quantidade por função, nunca
// pessoas nomeadas (decisão do brainstorming). QuantidadePreenchidas é incrementada por
// CadastrarPessoaTerceirizadaCommand (Task 8) a cada pessoa cadastrada nesta vaga.
public class ContratoVagaFuncao : AuditableEntity
{
    public Guid ContratoId { get; set; }
    public Contrato? Contrato { get; set; }

    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public int QuantidadeVagas { get; set; }
    public int QuantidadePreenchidas { get; set; }
}
