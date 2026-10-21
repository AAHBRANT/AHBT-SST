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
