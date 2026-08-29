using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Módulo de Requisitos Legais — Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29).
// Fase 1 (fundação de dados): só o cadastro estruturado do requisito e seus critérios de
// aplicabilidade. O cruzamento de fato (avaliar cada Obra contra estes critérios) é o Motor em si,
// ainda não implementado nesta fatia — ver plano combinado com o usuário.
//
// Conteúdo jurídico (qual norma, artigo, e que critério a torna aplicável) NÃO é gerado por este
// sistema — é cadastro manual de QSMS/Diretoria, validado por profissional habilitado. O sistema só
// fornece a ferramenta de cadastro e, depois, o motor que cruza contra o que já está no sistema
// (PGR/Perigos, Funções, AtivosSst, questionário por obra).
public class RequisitoLegal : AuditableEntity
{
    public string Norma { get; set; } = string.Empty; // ex.: "NR-35"
    public string? Artigo { get; set; } // ex.: "35.4"
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public CategoriaRequisitoLegal Categoria { get; set; }
    public StatusRequisitoLegal Status { get; set; } = StatusRequisitoLegal.Ativo;
    public string? Fonte { get; set; } // link/referência de onde o requisito foi extraído

    public ICollection<RequisitoLegalCriterio> Criterios { get; set; } = new List<RequisitoLegalCriterio>();
}

// Um requisito pode ter vários critérios; qualquer um satisfeito já basta para considerar o
// requisito aplicável a uma Obra (lógica OU) — decisão própria de escopo, sem seção literal
// correspondente. Exatamente um dos campos de referência (PerigoId/FuncaoId/TipoEquipamento/
// ItemQuestionarioAplicabilidadeId) é preenchido, de acordo com Tipo — não modelado como
// polimórfico solto (EntidadeTipo/EntidadeId) porque os quatro tipos de referência têm naturezas
// diferentes (duas são FK reais, uma é enum, uma é FK para outra tabela nova deste mesmo módulo) e
// FKs reais permitem íntegridade referencial de verdade nos três primeiros casos.
public class RequisitoLegalCriterio : AuditableEntity
{
    public Guid RequisitoLegalId { get; set; }
    public RequisitoLegal? RequisitoLegal { get; set; }

    public TipoCriterioAplicabilidade Tipo { get; set; }

    public Guid? PerigoId { get; set; }
    public Perigo? Perigo { get; set; }

    public Guid? FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public TipoAtivo? TipoEquipamento { get; set; }

    public Guid? ItemQuestionarioAplicabilidadeId { get; set; }
    public ItemQuestionarioAplicabilidade? ItemQuestionarioAplicabilidade { get; set; }
}
