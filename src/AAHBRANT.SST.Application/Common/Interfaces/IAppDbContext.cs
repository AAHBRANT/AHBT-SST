using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração que a Application depende (não referencia Infrastructure/EF Core diretamente,
// preservando a regra de dependência do Clean Architecture). Implementada por SstDbContext.
public interface IAppDbContext
{
    DbSet<Obra> Obras { get; }
    DbSet<Setor> Setores { get; }
    DbSet<Equipe> Equipes { get; }
    DbSet<Funcao> Funcoes { get; }
    DbSet<Trabalhador> Trabalhadores { get; }

    DbSet<PerfilAcesso> PerfisAcesso { get; }
    DbSet<Permissao> Permissoes { get; }
    DbSet<PerfilAcessoPermissao> PerfisAcessoPermissoes { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<UsuarioPerfilObra> UsuariosPerfilObra { get; }
    DbSet<TrilhaAuditoria> TrilhaAuditoria { get; }

    DbSet<Aso> Asos { get; }
    DbSet<ExameComplementar> ExamesComplementares { get; }
    DbSet<AptidaoAtividadeEspecifica> AptidoesAtividadeEspecifica { get; }
    DbSet<PcmsoDetalhe> PcmsoDetalhes { get; }
    DbSet<ExameFuncaoObra> ExamesFuncaoObra { get; }
    DbSet<PcmsoRevisao> PcmsoRevisoes { get; }
    DbSet<LeituraDocumentoIa> LeiturasDocumentoIa { get; }
    DbSet<CursoTreinamento> CursosTreinamento { get; }
    DbSet<Treinamento> Treinamentos { get; }
    DbSet<ArquivoCertificadoTreinamento> ArquivosCertificadoTreinamento { get; }
    DbSet<TermoCompromissoEpiManual> TermosCompromissoEpiManual { get; }
    DbSet<MatrizTreinamentoFuncao> MatrizTreinamentoFuncoes { get; }
    DbSet<SessaoTreinamento> SessoesTreinamento { get; }
    DbSet<ParticipanteSessaoTreinamento> ParticipantesSessaoTreinamento { get; }
    DbSet<FotoEvidenciaSessaoTreinamento> FotosEvidenciaSessaoTreinamento { get; }
    DbSet<CatalogoEpi> CatalogoEpis { get; }
    DbSet<EntregaEpi> EntregasEpi { get; }
    DbSet<MatrizEpiFuncao> MatrizEpiFuncoes { get; }
    DbSet<EstoqueEpi> EstoquesEpi { get; }
    DbSet<MovimentacaoEstoqueEpi> MovimentacoesEstoqueEpi { get; }
    DbSet<CatalogoEpc> CatalogoEpcs { get; }
    DbSet<InstalacaoEpc> InstalacoesEpc { get; }
    DbSet<EstoqueEpc> EstoquesEpc { get; }
    DbSet<MovimentacaoEstoqueEpc> MovimentacoesEstoqueEpc { get; }
    DbSet<CatalogoUniforme> CatalogoUniformes { get; }
    DbSet<EstoqueUniforme> EstoquesUniforme { get; }
    DbSet<MovimentacaoEstoqueUniforme> MovimentacoesEstoqueUniforme { get; }
    DbSet<MatrizUniformeFuncao> MatrizUniformeFuncoes { get; }
    DbSet<TrabalhadorTamanhoUniforme> TrabalhadorTamanhosUniforme { get; }
    DbSet<EntregaUniforme> EntregasUniforme { get; }
    DbSet<Alerta> Alertas { get; }
    DbSet<AlertaHistoricoEnvio> AlertaHistoricoEnvios { get; }
    DbSet<RegraAlerta> RegrasAlerta { get; }
    DbSet<CalendarioEventoTeams> CalendariosEventosTeams { get; }
    DbSet<Evidencia> Evidencias { get; }

    DbSet<Atividade> Atividades { get; }
    DbSet<Ghe> Ghes { get; }
    DbSet<GheFuncao> GheFuncoes { get; }
    DbSet<Perigo> Perigos { get; }
    DbSet<Risco> Riscos { get; }
    DbSet<RiscoTrabalhadorExposto> RiscoTrabalhadorExpostos { get; }
    DbSet<MatrizRiscoConfig> MatrizRiscoConfigs { get; }
    DbSet<MatrizRiscoCelula> MatrizRiscoCelulas { get; }

    DbSet<Pgr> Pgrs { get; }
    DbSet<PlanoAcaoItem> PlanoAcaoItens { get; }
    DbSet<PgrRevisao> PgrRevisoes { get; }

    DbSet<TagIdentificacao> TagsIdentificacao { get; }
    DbSet<AreaSst> AreasSst { get; }

    DbSet<Apr> Aprs { get; }
    DbSet<AprEtapa> AprEtapas { get; }
    DbSet<AprEtapaRisco> AprEtapaRiscos { get; }
    DbSet<AprResponsavel> AprResponsaveis { get; }
    DbSet<AprAssinatura> AprAssinaturas { get; }

    DbSet<PermissaoTrabalho> PermissoesTrabalho { get; }
    DbSet<PermissaoTrabalhoPreRequisito> PermissaoTrabalhoPreRequisitos { get; }
    DbSet<PermissaoTrabalhoTipoTrabalho> PermissaoTrabalhoTiposTrabalho { get; }
    DbSet<PermissaoTrabalhoVerificacao> PermissaoTrabalhoVerificacoes { get; }
    DbSet<PermissaoTrabalhoEpi> PermissaoTrabalhoEpis { get; }
    DbSet<PermissaoTrabalhoEpc> PermissaoTrabalhoEpcs { get; }
    DbSet<PermissaoTrabalhoRiscoCritico> PermissaoTrabalhoRiscosCriticos { get; }
    DbSet<PermissaoTrabalhoResponsavel> PermissaoTrabalhoResponsaveis { get; }

    DbSet<ChecklistModelo> ChecklistModelos { get; }
    DbSet<ChecklistModeloItem> ChecklistModeloItens { get; }
    DbSet<Inspecao> Inspecoes { get; }
    DbSet<InspecaoItemResposta> InspecaoItemRespostas { get; }

    DbSet<Alojamento> Alojamentos { get; }
    DbSet<AlojamentoMorador> AlojamentoMoradores { get; }
    DbSet<ConfiguracaoAlojamento> ConfiguracoesAlojamento { get; }
    DbSet<Veiculo> Veiculos { get; }

    // Qualificação explícita necessária: "Dds" sem prefixo é ambíguo aqui — a namespace
    // AAHBRANT.SST.Application.Dds (Commands/Queries deste módulo) sombreia o tipo importado por
    // using, já que ela é encontrada em um nível de namespace mais interno.
    DbSet<Domain.Entidades.Dds> Dds { get; }
    DbSet<DdsAtividade> DdsAtividades { get; }
    DbSet<DdsItemChecklist> DdsItensChecklist { get; }
    DbSet<DdsParticipante> DdsParticipantes { get; }
    DbSet<DdsFuncionarioSelecionado> DdsFuncionariosSelecionados { get; }
    DbSet<DdsSemanal> DdsSemanais { get; }
    DbSet<CatalogoTemaDds> CatalogosTemaDds { get; }
    DbSet<TemaDdsAgendado> TemasDdsAgendados { get; }
    DbSet<ReuniaoAnaliseOcorrencia> ReunioesAnaliseOcorrencia { get; }
    DbSet<DdsFotoEvidencia> DdsFotosEvidencia { get; }

    DbSet<NaoConformidade> NaoConformidades { get; }
    DbSet<AcaoPlano> AcoesPlano { get; }

    DbSet<Acidente> Acidentes { get; }
    DbSet<AcidenteFoto> AcidentesFotos { get; }
    DbSet<AcidenteEnvolvido> AcidentesEnvolvidos { get; }
    DbSet<RegistroHhtMensal> RegistrosHhtMensais { get; }

    DbSet<AtivoSst> AtivosSst { get; }

    DbSet<DocumentoAssinatura> DocumentosAssinatura { get; }
    DbSet<DocumentoSignatario> DocumentoSignatarios { get; }
    DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico { get; }
    DbSet<TemplateBiometricoFutronic> TemplatesBiometricoFutronic { get; }
    DbSet<FotoCadastroFacial> FotosCadastroFacial { get; }
    DbSet<FalhaReconhecimentoFacial> FalhasReconhecimentoFacial { get; }
    DbSet<RelatorioGerado> RelatoriosGerados { get; }
    DbSet<RelatorioEnvio> RelatorioEnvios { get; }
    DbSet<DestinatarioRelatorio> DestinatariosRelatorio { get; }
    DbSet<ExecucaoRelatorioAgendado> ExecucoesRelatorioAgendado { get; }

    DbSet<IdempotenciaRegistro> IdempotenciaRegistros { get; }

    DbSet<RequisitoLegal> RequisitosLegais { get; }
    DbSet<RequisitoLegalCriterio> RequisitoLegalCriterios { get; }
    DbSet<ItemQuestionarioAplicabilidade> ItensQuestionarioAplicabilidade { get; }
    DbSet<RespostaQuestionarioAplicabilidade> RespostasQuestionarioAplicabilidade { get; }

    DbSet<DimensionamentoCipa> DimensionamentosCipa { get; }
    DbSet<ProcessoEleitoralCipa> ProcessosEleitoraisCipa { get; }
    DbSet<CandidatoCipa> CandidatosCipa { get; }
    DbSet<MembroCipa> MembrosCipa { get; }
    DbSet<TreinamentoCipa> TreinamentosCipa { get; }
    DbSet<ReuniaoCipa> ReunioesCipa { get; }
    DbSet<ParticipanteReuniaoCipa> ParticipantesReuniaoCipa { get; }
    DbSet<InspecaoCipa> InspecoesCipa { get; }
    DbSet<EventoSipat> EventosSipat { get; }
    DbSet<AtividadeSipat> AtividadesSipat { get; }

    DbSet<MaterialApoio> MateriaisApoio { get; }

    DbSet<ContadorDocumento> ContadoresDocumento { get; }
    DbSet<SuporteIaSolicitacao> SuporteIaSolicitacoes { get; }
    DbSet<Empresa> Empresas { get; }
    DbSet<Contrato> Contratos { get; }
    DbSet<ContratoVagaFuncao> ContratoVagasFuncao { get; }

    DbSet<NovidadeVersao> NovidadesVersao { get; }
    DbSet<NovidadeVersaoItem> NovidadesVersaoItens { get; }
    DbSet<NovidadeVisualizacao> NovidadesVisualizacao { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Descarta entidades rastreadas que não foram salvas (ex.: após uma falha de SaveChangesAsync) —
    // usado por lotes que reusam o mesmo contexto entre itens independentes (ImportarColaboradoresGrhCommand),
    // onde uma entidade que falhou ao salvar ficaria presa no rastreamento e derrubaria toda tentativa
    // seguinte no mesmo lote.
    void DescartarAlteracoesPendentes();

    // Escopo por obra do usuário atual (o mesmo que o filtro global aplica), exposto para as
    // entidades que NÃO têm filtro global por ObraId e chegam à obra só pelo Trabalhador (Aso,
    // ExameComplementar, Aptidão, Treinamento, EntregaEpi...). Ficaram fora do filtro global de
    // propósito — filtrar pela navegação até Trabalhador esconde o histórico de desligados — e por
    // isso cada handler precisa aplicar o escopo explicitamente (auditoria de 09/10/2026: nenhum
    // aplicava). Use as extensões de Common/Seguranca/EscopoObra.cs em vez destes membros direto.
    bool EscopoObraGlobal { get; }
    IReadOnlyList<Guid> ObrasNoEscopo { get; }
}
