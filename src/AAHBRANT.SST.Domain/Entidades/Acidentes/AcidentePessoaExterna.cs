using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Pessoa que não é funcionária envolvida na ocorrência (terceiro, visitante, transeunte...).
// CPF e telefone são dados pessoais (LGPD): gravados criptografados (AES-GCM, mesmo conversor do CPF
// do trabalhador, sem hash) e devolvidos sempre mascarados; o valor em claro só sai por consulta
// auditada (ObterDadosPessoaisPessoaExternaQuery).
public class AcidentePessoaExterna : AuditableEntity
{
    public Guid AcidenteId { get; set; }
    public Acidente? Acidente { get; set; }

    public string Nome { get; set; } = string.Empty;
    public VinculoPessoaExterna Vinculo { get; set; }
    public string? Empresa { get; set; }
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public int? Idade { get; set; }
    public string? Lesao { get; set; }
    public string? Atendimento { get; set; }
    public ResultadoPessoaExterna Resultado { get; set; } = ResultadoPessoaExterna.SemLesao;
}
