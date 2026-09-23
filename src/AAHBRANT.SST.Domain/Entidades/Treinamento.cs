using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

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

    // Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — marca
    // qual curso do catálogo representa a "Integração de Segurança" obrigatória para TODO
    // terceirizado, independente da função (regra fixa do módulo, não depende de
    // MatrizTreinamentoFuncao). No máximo um curso com true por vez — ver
    // AtualizarCursoTreinamentoCommandHandler/CriarCursoTreinamentoCommandHandler.
    public bool EhIntegracaoSeguranca { get; set; }

    // Marcador explícito de "este curso atende à NR-06 (uso de EPI)" — pedido do usuário em 22/09.
    // Antes a entrega de EPI adivinhava isso pelo texto de NormaReferencia (campo livre: bastava
    // alguém digitar "NR-06 e NR-18" para o curso deixar de ser reconhecido e a obra inteira travar
    // na entrega de EPI). A migration marca sozinha os cursos cuja NormaReferencia já resolvia para
    // 6, então nada muda para o que já estava cadastrado.
    public bool AtendeNr6 { get; set; }

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

    // Campos do certificado de treinamento reformulado (pedido do usuário, 06/09). Local: onde o
    // treinamento foi realizado (obra, centro de treinamento, EaD) — sem preencher, o certificado usa
    // o nome da Obra do trabalhador como já fazia antes. InstrutorRegistroProfissional: registro
    // profissional (CREA/MTE) do instrutor — sempre o Técnico de Segurança do Trabalho responsável,
    // que também assina como Responsável Técnico (decisão do usuário: uma assinatura só, não três).
    public string? Local { get; set; }
    public string? InstrutorRegistroProfissional { get; set; }

    // Preenchido só quando este Treinamento foi gerado pelo encerramento de uma turma (04/09) —
    // nulo para registros criados manualmente por trabalhador (fluxo antigo, que continua existindo).
    // Usado para o certificado individual buscar as fotos/evidências da turma (ver
    // ExportarCertificadoTreinamentoQuery).
    public Guid? SessaoTreinamentoId { get; set; }
    public SessaoTreinamento? SessaoTreinamento { get; set; }

    // Lançamento retroativo (22/09). Aahbrant = curso ministrado pela empresa, o sistema emite o
    // certificado no modelo próprio como sempre fez. Externo = curso feito por terceiro, só
    // registrado aqui: a emissão do modelo AAHBRANT é recusada e o download entrega o arquivo
    // original anexado. Default Aahbrant preserva o comportamento de todo registro já existente.
    // Nome não é "Origem" porque AuditableEntity já usa essa propriedade (OrigemRegistro).
    public OrigemCertificadoTreinamento OrigemCertificado { get; set; } = OrigemCertificadoTreinamento.Aahbrant;

    public ArquivoCertificadoTreinamento? ArquivoCertificado { get; set; }

    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}

// Certificado digitalizado (PDF ou foto do papel) de um Treinamento — pedido do usuário em 22/09
// para o lançamento retroativo da obra em andamento. Tabela própria, e não colunas dentro de
// Treinamento, porque várias telas materializam a entidade Treinamento inteira (perfil do
// trabalhador, motor de alertas, emissão do certificado) e arrastar megabytes de arquivo em cada
// uma dessas consultas degradaria todas elas. Mesmo padrão de bytes-no-banco já usado em
// MaterialApoio.Conteudo e FotoEvidenciaSessaoTreinamento.FotoConteudo.
public class ArquivoCertificadoTreinamento : AuditableEntity
{
    public Guid TreinamentoId { get; set; }
    public Treinamento? Treinamento { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public long TamanhoBytes { get; set; }
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
