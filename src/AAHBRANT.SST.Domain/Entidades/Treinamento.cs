using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

public class CursoTreinamento : AuditableEntity
{
    public string Nome { get; set; } = string.Empty; // ex.: "NR-35 Trabalho em Altura"
    public string? NormaReferencia { get; set; }
    public int CargaHorariaMinima { get; set; }
    public int ValidadeEmMeses { get; set; }

    // Verso do certificado (modelo AAHBRANT em PLANILHA -MODELO RISCOS-FUNÇOES-NRS-CERTIFICADOS.XLSX,
    // abas "CERTIFICADO DE NR XX"): lista de tópicos do curso, um por linha. Opcional — cursos sem
    // conteúdo cadastrado simplesmente não geram a página de conteúdo programático no certificado.
    public string? ConteudoProgramatico { get; set; }

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

// Matriz de Treinamento por Função (PR-SST-002, item 1): vínculo Função↔CursoTreinamento,
// mesmo padrão estrutural de MatrizEpiFuncao (Epi.cs) — sync idempotente via Ativo.
public class MatrizTreinamentoFuncao : AuditableEntity
{
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }

    public Guid CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }
}
