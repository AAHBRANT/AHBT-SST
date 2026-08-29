using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class CursoTreinamento : AuditableEntity
{
    public string Nome { get; set; } = string.Empty; // ex.: "NR-35 Trabalho em Altura"
    public string? NormaReferencia { get; set; }
    public int CargaHorariaMinima { get; set; }
    public int ValidadeEmMeses { get; set; }

    public ICollection<Treinamento> Realizacoes { get; set; } = new List<Treinamento>();
}

public class Treinamento : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public Guid CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }

    public DateTime DataRealizacao { get; set; }
    public DateTime DataValidade { get; set; }
    public int CargaHorariaRealizada { get; set; }
    public string? InstituicaoInstrutor { get; set; }
    public string? NumeroCertificado { get; set; }

    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}

// Matriz de obrigatoriedade de treinamento por função — mesmo princípio de MatrizEpiFuncao (Epi.cs):
// define quais cursos são obrigatórios para cada função, distinto de Treinamento (que registra a
// realização de fato por trabalhador). Base para o Motor de Aplicabilidade Legal gerar/gerenciar
// treinamentos obrigatórios a partir de um RequisitoLegal aplicável.
public class MatrizTreinamentoFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }
}
