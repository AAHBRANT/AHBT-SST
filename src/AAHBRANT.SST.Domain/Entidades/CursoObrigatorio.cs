using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Cursos obrigatórios a partir do PGR (21/10, desenho aprovado pelo usuário): o PGR da obra é a
// fonte. Cada regra diz quando uma NR passa a ser obrigatória — para todas as funções (NR-06,
// NR-18) ou quando o PGR tiver uma das palavras-chave nos perigos/atividades/ferramentas da função.
// Tabela editável pelo QSMS; a carga inicial (RegrasCursoObrigatorioSeeder) é proposta do sistema.
public class RegraCursoObrigatorio : AuditableEntity
{
    // "NR-35". O curso do catálogo é resolvido pela NormaReferencia quando CursoTreinamentoId é nulo,
    // para a regra funcionar em banco onde o curso ainda não foi cadastrado.
    public string Norma { get; set; } = string.Empty;
    public Guid? CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public bool AplicaTodasFuncoes { get; set; }

    // Separadas por vírgula; comparadas sem acento e sem caixa.
    public string? PalavrasChave { get; set; }
}

// Curso obrigatório de uma função NAQUELA obra (o mesmo "Pedreiro" pode trabalhar em altura numa
// obra e não em outra). Nasce Sugerido pela geração a partir do PGR, ou Aprovado quando adicionado
// à mão; só Aprovado conta para a aptidão. Recusado fica registrado e a regeração não o sugere de novo.
public class CursoObrigatorioFuncaoObra : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public Guid FuncaoId { get; set; }
    public Funcao? Funcao { get; set; }
    public Guid CursoTreinamentoId { get; set; }
    public CursoTreinamento? CursoTreinamento { get; set; }

    public StatusCursoObrigatorio Status { get; set; } = StatusCursoObrigatorio.Sugerido;
    public OrigemCursoObrigatorio OrigemSugestao { get; set; } = OrigemCursoObrigatorio.Pgr;

    // Por quê, tirado do PGR: "Perigo \"Queda de altura\" na atividade 3.2 Montagem de armação".
    public string? Motivo { get; set; }
    public Guid? PgrId { get; set; }

    public string? JustificativaRecusa { get; set; }
    public Guid? DecididoPorUsuarioId { get; set; }
    public string? DecididoPorNome { get; set; }
    public DateTime? DecididoEmUtc { get; set; }
}
