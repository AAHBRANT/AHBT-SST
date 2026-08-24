using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Não conformidade (Base de Conhecimento §25, linhas 620-643). Campos literais do documento:
// origem; requisito relacionado; descrição; local; atividade; risco; evidência; responsável;
// prazo; ação corretiva; ação preventiva; status; data de conclusão; evidência de encerramento.
//
// Decisões não-literais assumidas (a confirmar com o usuário se ele quiser outro comportamento):
// - "ação corretiva"/"ação preventiva" não viram dois campos de texto — cada ação vira uma linha
//   em AcaoPlano (OrigemTipo=nameof(NaoConformidade), OrigemId=Id), reaproveitando o mesmo módulo
//   genérico que futuros módulos (Acidentes, Auditorias) também usarão.
// - "evidência" e "evidência de encerramento": o sistema já possui uma tabela genérica Evidencia
//   (EntidadeTipo/EntidadeId), mas ela não tem nenhum controller/uso real em NENHUM módulo do
//   sistema (gap pré-existente, não introduzido aqui). Em vez de inventar um anexo que não pode
//   ser de fato enviado por nenhuma tela, adicionamos apenas ObservacoesEncerramento (texto),
//   análogo ao campo já usado em PermissaoTrabalho.ObservacoesEncerramento (§18).
public class NaoConformidade : AuditableEntity
{
    // Renomeado para "OrigemDeteccao" (não "Origem") para não colidir/ocultar
    // AuditableEntity.Origem (OrigemRegistro: Manual/Importacao/Ocr/IntegracaoGraph — conceito
    // diferente, de auditoria de criação do registro, não o "origem" literal do §25).
    public OrigemNaoConformidade OrigemDeteccao { get; set; }
    public string? RequisitoRelacionado { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Local { get; set; }

    public Guid? AtividadeId { get; set; }
    public Atividade? Atividade { get; set; }

    public Guid? RiscoId { get; set; }
    public Risco? Risco { get; set; }

    public Guid? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public DateTime? Prazo { get; set; }

    public StatusNaoConformidade Status { get; set; } = StatusNaoConformidade.Aberta;

    public DateTime? DataConclusao { get; set; }
    public string? ObservacoesEncerramento { get; set; }
}
