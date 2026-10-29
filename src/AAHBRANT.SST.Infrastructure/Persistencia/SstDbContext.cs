using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.NaoConformidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Persistencia;

public class SstDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService _usuarioAtual;

    public SstDbContext(DbContextOptions<SstDbContext> options, ICurrentUserService usuarioAtual) : base(options)
    {
        _usuarioAtual = usuarioAtual;
    }

    public DbSet<Obra> Obras => Set<Obra>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<Funcao> Funcoes => Set<Funcao>();
    public DbSet<Trabalhador> Trabalhadores => Set<Trabalhador>();

    public DbSet<PerfilAcesso> PerfisAcesso => Set<PerfilAcesso>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<PerfilAcessoPermissao> PerfisAcessoPermissoes => Set<PerfilAcessoPermissao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPerfilObra> UsuariosPerfilObra => Set<UsuarioPerfilObra>();

    public DbSet<Aso> Asos => Set<Aso>();
    public DbSet<AsoRestricao> AsoRestricoes => Set<AsoRestricao>();
    public DbSet<ExameComplementar> ExamesComplementares => Set<ExameComplementar>();
    public DbSet<AptidaoAtividadeEspecifica> AptidoesAtividadeEspecifica => Set<AptidaoAtividadeEspecifica>();
    public DbSet<PcmsoDetalhe> PcmsoDetalhes => Set<PcmsoDetalhe>();
    public DbSet<ExameFuncaoObra> ExamesFuncaoObra => Set<ExameFuncaoObra>();
    public DbSet<PcmsoRevisao> PcmsoRevisoes => Set<PcmsoRevisao>();
    public DbSet<LeituraDocumentoIa> LeiturasDocumentoIa => Set<LeituraDocumentoIa>();
    public DbSet<CursoTreinamento> CursosTreinamento => Set<CursoTreinamento>();
    public DbSet<Treinamento> Treinamentos => Set<Treinamento>();
    public DbSet<ArquivoCertificadoTreinamento> ArquivosCertificadoTreinamento => Set<ArquivoCertificadoTreinamento>();
    public DbSet<ArquivoAso> ArquivosAso => Set<ArquivoAso>();
    public DbSet<TermoCompromissoEpiManual> TermosCompromissoEpiManual => Set<TermoCompromissoEpiManual>();
    public DbSet<MatrizTreinamentoFuncao> MatrizTreinamentoFuncoes => Set<MatrizTreinamentoFuncao>();
    public DbSet<RegraCursoObrigatorio> RegrasCursoObrigatorio => Set<RegraCursoObrigatorio>();
    public DbSet<CursoObrigatorioFuncaoObra> CursosObrigatoriosFuncaoObra => Set<CursoObrigatorioFuncaoObra>();
    public DbSet<SessaoTreinamento> SessoesTreinamento => Set<SessaoTreinamento>();
    public DbSet<ParticipanteSessaoTreinamento> ParticipantesSessaoTreinamento => Set<ParticipanteSessaoTreinamento>();
    public DbSet<FotoEvidenciaSessaoTreinamento> FotosEvidenciaSessaoTreinamento => Set<FotoEvidenciaSessaoTreinamento>();
    public DbSet<CatalogoEpi> CatalogoEpis => Set<CatalogoEpi>();
    public DbSet<EntregaEpi> EntregasEpi => Set<EntregaEpi>();
    public DbSet<MatrizEpiFuncao> MatrizEpiFuncoes => Set<MatrizEpiFuncao>();
    public DbSet<EstoqueEpi> EstoquesEpi => Set<EstoqueEpi>();
    public DbSet<MovimentacaoEstoqueEpi> MovimentacoesEstoqueEpi => Set<MovimentacaoEstoqueEpi>();
    public DbSet<ReservaEpi> ReservasEpi => Set<ReservaEpi>();
    public DbSet<PlanoIntegracao> PlanosIntegracao => Set<PlanoIntegracao>();
    public DbSet<SolicitacaoReposicaoEpi> SolicitacoesReposicaoEpi => Set<SolicitacaoReposicaoEpi>();
    public DbSet<ArquivoContrato> ArquivosContrato => Set<ArquivoContrato>();
    public DbSet<DocumentoExigidoEmpresa> DocumentosExigidosEmpresa => Set<DocumentoExigidoEmpresa>();
    public DbSet<DocumentoEmpresaTerceirizada> DocumentosEmpresaTerceirizada => Set<DocumentoEmpresaTerceirizada>();
    public DbSet<ArquivoDocumentoEmpresaTerceirizada> ArquivosDocumentoEmpresaTerceirizada => Set<ArquivoDocumentoEmpresaTerceirizada>();
    public DbSet<CatalogoEpc> CatalogoEpcs => Set<CatalogoEpc>();
    public DbSet<InstalacaoEpc> InstalacoesEpc => Set<InstalacaoEpc>();
    public DbSet<EstoqueEpc> EstoquesEpc => Set<EstoqueEpc>();
    public DbSet<MovimentacaoEstoqueEpc> MovimentacoesEstoqueEpc => Set<MovimentacaoEstoqueEpc>();
    public DbSet<CatalogoUniforme> CatalogoUniformes => Set<CatalogoUniforme>();
    public DbSet<EstoqueUniforme> EstoquesUniforme => Set<EstoqueUniforme>();
    public DbSet<MovimentacaoEstoqueUniforme> MovimentacoesEstoqueUniforme => Set<MovimentacaoEstoqueUniforme>();
    public DbSet<MatrizUniformeFuncao> MatrizUniformeFuncoes => Set<MatrizUniformeFuncao>();
    public DbSet<TrabalhadorTamanhoUniforme> TrabalhadorTamanhosUniforme => Set<TrabalhadorTamanhoUniforme>();
    public DbSet<EntregaUniforme> EntregasUniforme => Set<EntregaUniforme>();

    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AlertaHistoricoEnvio> AlertaHistoricoEnvios => Set<AlertaHistoricoEnvio>();
    public DbSet<RegraAlerta> RegrasAlerta => Set<RegraAlerta>();
    public DbSet<CalendarioEventoTeams> CalendariosEventosTeams => Set<CalendarioEventoTeams>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<TrilhaAuditoria> TrilhaAuditoria => Set<TrilhaAuditoria>();

    public DbSet<Atividade> Atividades => Set<Atividade>();
    public DbSet<AtividadeEpc> AtividadeEpcs => Set<AtividadeEpc>();
    public DbSet<AtividadeDocumento> AtividadeDocumentos => Set<AtividadeDocumento>();
    public DbSet<AtividadeItemOs> AtividadeItensOs => Set<AtividadeItemOs>();
    public DbSet<Ghe> Ghes => Set<Ghe>();
    public DbSet<GheFuncao> GheFuncoes => Set<GheFuncao>();
    public DbSet<Perigo> Perigos => Set<Perigo>();
    public DbSet<Risco> Riscos => Set<Risco>();
    public DbSet<MedidaPrevencao> MedidasPrevencao => Set<MedidaPrevencao>();
    public DbSet<RiscoTrabalhadorExposto> RiscoTrabalhadorExpostos => Set<RiscoTrabalhadorExposto>();
    public DbSet<MatrizRiscoConfig> MatrizRiscoConfigs => Set<MatrizRiscoConfig>();
    public DbSet<MatrizRiscoCelula> MatrizRiscoCelulas => Set<MatrizRiscoCelula>();

    public DbSet<Pgr> Pgrs => Set<Pgr>();
    public DbSet<GeracaoPgrEap> GeracoesPgrEap => Set<GeracaoPgrEap>();
    public DbSet<PlanoAcaoItem> PlanoAcaoItens => Set<PlanoAcaoItem>();
    public DbSet<PgrRevisao> PgrRevisoes => Set<PgrRevisao>();

    public DbSet<TagIdentificacao> TagsIdentificacao => Set<TagIdentificacao>();
    public DbSet<AreaSst> AreasSst => Set<AreaSst>();

    public DbSet<Apr> Aprs => Set<Apr>();
    public DbSet<AprEtapa> AprEtapas => Set<AprEtapa>();
    public DbSet<AprEtapaRisco> AprEtapaRiscos => Set<AprEtapaRisco>();
    public DbSet<AprResponsavel> AprResponsaveis => Set<AprResponsavel>();
    public DbSet<AprAssinatura> AprAssinaturas => Set<AprAssinatura>();

    public DbSet<PermissaoTrabalho> PermissoesTrabalho => Set<PermissaoTrabalho>();
    public DbSet<PermissaoTrabalhoPreRequisito> PermissaoTrabalhoPreRequisitos => Set<PermissaoTrabalhoPreRequisito>();
    public DbSet<PermissaoTrabalhoTipoTrabalho> PermissaoTrabalhoTiposTrabalho => Set<PermissaoTrabalhoTipoTrabalho>();
    public DbSet<PermissaoTrabalhoVerificacao> PermissaoTrabalhoVerificacoes => Set<PermissaoTrabalhoVerificacao>();
    public DbSet<PermissaoTrabalhoEpi> PermissaoTrabalhoEpis => Set<PermissaoTrabalhoEpi>();
    public DbSet<PermissaoTrabalhoEpc> PermissaoTrabalhoEpcs => Set<PermissaoTrabalhoEpc>();
    public DbSet<PermissaoTrabalhoRiscoCritico> PermissaoTrabalhoRiscosCriticos => Set<PermissaoTrabalhoRiscoCritico>();
    public DbSet<PermissaoTrabalhoResponsavel> PermissaoTrabalhoResponsaveis => Set<PermissaoTrabalhoResponsavel>();

    public DbSet<ChecklistModelo> ChecklistModelos => Set<ChecklistModelo>();
    public DbSet<ChecklistModeloItem> ChecklistModeloItens => Set<ChecklistModeloItem>();
    public DbSet<Inspecao> Inspecoes => Set<Inspecao>();
    public DbSet<InspecaoItemResposta> InspecaoItemRespostas => Set<InspecaoItemResposta>();

    public DbSet<Alojamento> Alojamentos => Set<Alojamento>();
    public DbSet<AlojamentoMorador> AlojamentoMoradores => Set<AlojamentoMorador>();
    public DbSet<ConfiguracaoAlojamento> ConfiguracoesAlojamento => Set<ConfiguracaoAlojamento>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<TemaInspecao> TemasInspecao => Set<TemaInspecao>();
    public DbSet<ItemInspecionavel> ItensInspecionaveis => Set<ItemInspecionavel>();

    public DbSet<Dds> Dds => Set<Dds>();
    public DbSet<DdsAtividade> DdsAtividades => Set<DdsAtividade>();
    public DbSet<DdsItemChecklist> DdsItensChecklist => Set<DdsItemChecklist>();
    public DbSet<DdsParticipante> DdsParticipantes => Set<DdsParticipante>();
    public DbSet<DdsFuncionarioSelecionado> DdsFuncionariosSelecionados => Set<DdsFuncionarioSelecionado>();
    public DbSet<DdsSemanal> DdsSemanais => Set<DdsSemanal>();
    public DbSet<CronogramaMensalDds> CronogramasMensaisDds => Set<CronogramaMensalDds>();
    public DbSet<CronogramaMensalDdsRevisao> CronogramasMensaisDdsRevisoes => Set<CronogramaMensalDdsRevisao>();
    public DbSet<CatalogoTemaDds> CatalogosTemaDds => Set<CatalogoTemaDds>();
    public DbSet<TemaDdsAgendado> TemasDdsAgendados => Set<TemaDdsAgendado>();
    public DbSet<ReuniaoAnaliseOcorrencia> ReunioesAnaliseOcorrencia => Set<ReuniaoAnaliseOcorrencia>();
    public DbSet<DdsFotoEvidencia> DdsFotosEvidencia => Set<DdsFotoEvidencia>();

    public DbSet<NaoConformidade> NaoConformidades => Set<NaoConformidade>();
    public DbSet<EventoNcGQuali> EventosNcGQuali => Set<EventoNcGQuali>();
    public DbSet<AcaoPlano> AcoesPlano => Set<AcaoPlano>();

    public DbSet<Acidente> Acidentes => Set<Acidente>();
    public DbSet<AcidenteFoto> AcidentesFotos => Set<AcidenteFoto>();
    public DbSet<AcidenteEnvolvido> AcidentesEnvolvidos => Set<AcidenteEnvolvido>();
    public DbSet<RegistroHhtMensal> RegistrosHhtMensais => Set<RegistroHhtMensal>();

    public DbSet<AtivoSst> AtivosSst => Set<AtivoSst>();

    public DbSet<DocumentoAssinatura> DocumentosAssinatura => Set<DocumentoAssinatura>();
    public DbSet<DocumentoSignatario> DocumentoSignatarios => Set<DocumentoSignatario>();
    public DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico => Set<DispositivoAgenteBiometrico>();
    public DbSet<DesafioBiometria> DesafiosBiometria => Set<DesafioBiometria>();
    public DbSet<TemplateBiometricoFutronic> TemplatesBiometricoFutronic => Set<TemplateBiometricoFutronic>();
    public DbSet<PerfilInstrutor> PerfisInstrutor => Set<PerfilInstrutor>();
    public DbSet<FotoCadastroFacial> FotosCadastroFacial => Set<FotoCadastroFacial>();
    public DbSet<FalhaReconhecimentoFacial> FalhasReconhecimentoFacial => Set<FalhaReconhecimentoFacial>();
    public DbSet<RelatorioGerado> RelatoriosGerados => Set<RelatorioGerado>();
    public DbSet<RelatorioEnvio> RelatorioEnvios => Set<RelatorioEnvio>();
    public DbSet<DestinatarioRelatorio> DestinatariosRelatorio => Set<DestinatarioRelatorio>();
    public DbSet<ExecucaoRelatorioAgendado> ExecucoesRelatorioAgendado => Set<ExecucaoRelatorioAgendado>();

    public DbSet<IdempotenciaRegistro> IdempotenciaRegistros => Set<IdempotenciaRegistro>();

    public DbSet<RequisitoLegal> RequisitosLegais => Set<RequisitoLegal>();
    public DbSet<RequisitoLegalCriterio> RequisitoLegalCriterios => Set<RequisitoLegalCriterio>();
    public DbSet<ItemQuestionarioAplicabilidade> ItensQuestionarioAplicabilidade => Set<ItemQuestionarioAplicabilidade>();
    public DbSet<RespostaQuestionarioAplicabilidade> RespostasQuestionarioAplicabilidade => Set<RespostaQuestionarioAplicabilidade>();

    public DbSet<DimensionamentoCipa> DimensionamentosCipa => Set<DimensionamentoCipa>();
    public DbSet<ProcessoEleitoralCipa> ProcessosEleitoraisCipa => Set<ProcessoEleitoralCipa>();
    public DbSet<CandidatoCipa> CandidatosCipa => Set<CandidatoCipa>();
    public DbSet<MembroCipa> MembrosCipa => Set<MembroCipa>();
    public DbSet<TreinamentoCipa> TreinamentosCipa => Set<TreinamentoCipa>();
    public DbSet<ReuniaoCipa> ReunioesCipa => Set<ReuniaoCipa>();
    public DbSet<ParticipanteReuniaoCipa> ParticipantesReuniaoCipa => Set<ParticipanteReuniaoCipa>();
    public DbSet<InspecaoCipa> InspecoesCipa => Set<InspecaoCipa>();
    public DbSet<EventoSipat> EventosSipat => Set<EventoSipat>();
    public DbSet<AtividadeSipat> AtividadesSipat => Set<AtividadeSipat>();

    public DbSet<MaterialApoio> MateriaisApoio => Set<MaterialApoio>();

    public DbSet<ContadorDocumento> ContadoresDocumento => Set<ContadorDocumento>();
    public DbSet<SuporteIaSolicitacao> SuporteIaSolicitacoes => Set<SuporteIaSolicitacao>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ContratoVagaFuncao> ContratoVagasFuncao => Set<ContratoVagaFuncao>();

    public DbSet<NovidadeVersao> NovidadesVersao => Set<NovidadeVersao>();
    public DbSet<NovidadeVersaoItem> NovidadesVersaoItens => Set<NovidadeVersaoItem>();
    public DbSet<NovidadeVisualizacao> NovidadesVisualizacao => Set<NovidadeVisualizacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SstDbContext).Assembly);

        // Sincronização offline: RowVersion (já existente em AuditableEntity para todas as
        // entidades, mas até aqui nunca configurado) passa a ser o token de concorrência otimista.
        // Necessário para o app de campo detectar quando um registro editado offline mudou no
        // servidor nesse meio tempo (ver docs/RBAC-Matrix.md e o middleware de conflito em
        // TratamentoDeExcecaoMiddleware). Exige migration (ALTER COLUMN para "rowversion") antes de
        // ter efeito real no banco — ver instruções no PR.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.RowVersion)).IsRowVersion();
            }
        }

        // Camada 3 do RBAC (docs/RBAC-Matrix.md §4, "Global Query Filter... mitiga BOLA"): as
        // entidades abaixo têm ObraId direto na própria tabela — são o alvo do filtro. Cada uma
        // SUBSTITUI (não acumula com) o HasQueryFilter(x => x.Ativo) já registrado por sua própria
        // Configuracao logo acima (EF Core só guarda um filtro por entidade). Por isso a condição
        // Ativo é repetida aqui explicitamente, senão o soft-delete deixaria de funcionar. Obra
        // entra no mesmo filtro para que api.obras.listar() exponha apenas as obras do perfil em
        // uso; esse retorno é a base para a UI travar automaticamente quando só houver uma obra.
        // Sem efeito hoje (TemAcessoGlobal fica true enquanto a autenticação
        // Entra ID não estiver configurada — ver EscopoPorObraMiddleware): a consulta gerada é
        // idêntica à de antes até a autenticação real entrar em vigor.
        modelBuilder.Entity<Obra>().HasQueryFilter(o =>
            o.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(o.Id)));
        modelBuilder.Entity<Alerta>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || (a.ObraId.HasValue && _usuarioAtual.ObrasPermitidas.Contains(a.ObraId.Value))));
        modelBuilder.Entity<AtivoSst>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<Dds>().HasQueryFilter(d =>
            d.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(d.ObraId)));
        modelBuilder.Entity<CronogramaMensalDds>().HasQueryFilter(c =>
            c.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(c.ObraId)));
        modelBuilder.Entity<CronogramaMensalDdsRevisao>().HasQueryFilter(r =>
            r.Ativo && r.Cronograma!.Ativo &&
            (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(r.Cronograma.ObraId)));
        modelBuilder.Entity<TemaDdsAgendado>().HasQueryFilter(t =>
            t.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(t.ObraId)));
        modelBuilder.Entity<ReuniaoAnaliseOcorrencia>().HasQueryFilter(r =>
            r.Ativo && r.Acidente!.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(r.Acidente.ObraId)));
        modelBuilder.Entity<DdsFuncionarioSelecionado>().HasQueryFilter(s =>
            s.Ativo && s.Dds!.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(s.Dds.ObraId)));
        modelBuilder.Entity<DdsSemanal>().HasQueryFilter(d =>
            d.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(d.ObraId)));
        modelBuilder.Entity<Inspecao>().HasQueryFilter(i =>
            i.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(i.ObraId)));
        modelBuilder.Entity<AcidenteFoto>().HasQueryFilter(f =>
            f.Ativo && f.Acidente!.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(f.Acidente.ObraId)));
        modelBuilder.Entity<AcidenteEnvolvido>().HasQueryFilter(e =>
            e.Ativo && e.Acidente!.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.Acidente.ObraId)));
        modelBuilder.Entity<Acidente>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<RegistroHhtMensal>().HasQueryFilter(r =>
            r.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(r.ObraId)));
        modelBuilder.Entity<Pgr>().HasQueryFilter(p =>
            p.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(p.ObraId)));
        modelBuilder.Entity<PcmsoDetalhe>().HasQueryFilter(p =>
            p.Ativo && (_usuarioAtual.TemAcessoGlobal || (p.ObraId.HasValue && _usuarioAtual.ObrasPermitidas.Contains(p.ObraId.Value))));
        modelBuilder.Entity<Atividade>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<Ghe>().HasQueryFilter(g =>
            g.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(g.ObraId)));
        modelBuilder.Entity<CursoObrigatorioFuncaoObra>().HasQueryFilter(c =>
            c.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(c.ObraId)));
        modelBuilder.Entity<ExameFuncaoObra>().HasQueryFilter(e =>
            e.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.ObraId)));
        modelBuilder.Entity<LeituraDocumentoIa>().HasQueryFilter(l =>
            l.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(l.ObraId)));
        modelBuilder.Entity<Setor>().HasQueryFilter(s =>
            s.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(s.ObraId)));
        // Relatório de uma obra só aparece para quem tem acesso a ela; o consolidado (ObraId nulo) só para
        // quem tem acesso global.
        modelBuilder.Entity<RelatorioGerado>().HasQueryFilter(r =>
            r.Ativo && (_usuarioAtual.TemAcessoGlobal || (r.ObraId.HasValue && _usuarioAtual.ObrasPermitidas.Contains(r.ObraId.Value))));
        modelBuilder.Entity<FalhaReconhecimentoFacial>().HasQueryFilter(f =>
            f.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(f.ObraId)));
        modelBuilder.Entity<Trabalhador>().HasQueryFilter(t =>
            t.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(t.ObraId)));
        modelBuilder.Entity<AreaSst>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<EstoqueEpi>().HasQueryFilter(e =>
            e.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.ObraId)));
        modelBuilder.Entity<RespostaQuestionarioAplicabilidade>().HasQueryFilter(r =>
            r.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(r.ObraId)));
        modelBuilder.Entity<DimensionamentoCipa>().HasQueryFilter(d =>
            d.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(d.ObraId)));
        modelBuilder.Entity<ProcessoEleitoralCipa>().HasQueryFilter(p =>
            p.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(p.ObraId)));
        modelBuilder.Entity<MembroCipa>().HasQueryFilter(m =>
            m.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(m.ObraId)));
        modelBuilder.Entity<ReuniaoCipa>().HasQueryFilter(r =>
            r.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(r.ObraId)));
        modelBuilder.Entity<InspecaoCipa>().HasQueryFilter(i =>
            i.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(i.ObraId)));
        modelBuilder.Entity<EventoSipat>().HasQueryFilter(e =>
            e.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.ObraId)));
        modelBuilder.Entity<Alojamento>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<SolicitacaoReposicaoEpi>().HasQueryFilter(s =>
            s.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(s.ObraId)));
        modelBuilder.Entity<PlanoIntegracao>().HasQueryFilter(p =>
            p.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(p.ObraId)));
        modelBuilder.Entity<Veiculo>().HasQueryFilter(v =>
            v.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(v.ObraId)));
        modelBuilder.Entity<ItemInspecionavel>().HasQueryFilter(i =>
            i.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(i.ObraId)));
        // ASO NÃO tem filtro global por obra (tentado em #115 e revertido): o ASO só chega à obra pelo
        // Trabalhador, e navegar até ele dentro do filtro faz o filtro de Trabalhador (Ativo) valer
        // também — o ASO de quem foi desligado sumia das telas, até para acesso global (comprovado em
        // teste no CI). O ASO precisa ser guardado e consultável por anos (fiscalização). O escopo por
        // obra do ASO exige gravar ObraId no próprio registro (migration + preenchimento) — pendente.
        // Exame/Aptidão/Treinamento têm a mesma limitação e também ficam sem filtro por enquanto.
        // Registros que já têm ObraId próprio. Auditoria 06/10/2026 (A2), mesmo padrão dos demais filtros.
        // Exame/Aptidão/Treinamento NÃO entram aqui de propósito: eles só chegam à obra pelo
        // Trabalhador, e navegar até ele dentro do filtro faz o filtro de Trabalhador (Ativo) valer
        // também, escondendo o histórico de quem foi desligado (comprovado em teste). Ver PR #116.
        modelBuilder.Entity<SessaoTreinamento>().HasQueryFilter(s =>
            s.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(s.ObraId)));
        modelBuilder.Entity<SessaoTreinamento>()
            .HasOne(s => s.InstrutorTrabalhador).WithMany()
            .HasForeignKey(s => s.InstrutorTrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Contrato>().HasQueryFilter(c =>
            c.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(c.ObraId)));
        modelBuilder.Entity<InstalacaoEpc>().HasQueryFilter(i =>
            i.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(i.ObraId)));
        modelBuilder.Entity<EstoqueEpc>().HasQueryFilter(e =>
            e.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.ObraId)));
        modelBuilder.Entity<EstoqueUniforme>().HasQueryFilter(e =>
            e.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(e.ObraId)));
        // ExameComplementar/AptidaoAtividadeEspecifica/EntregaEpi e filhos de entidades escopadas
        // dependem de navegação por Trabalhador/Estoque/Membro/Reunião. A proteção fica nos handlers,
        // que precisam aplicar o escopo EXPLICITAMENTE com Application/Common/Seguranca/EscopoObra.cs
        // (e Assinatura/EscopoDocumentoAssinatura.cs para o Motor de Assinatura). A auditoria de
        // 09/10/2026 achou que nenhum handler fazia isso: ASO/exame/aptidão/documentos corrigidos;
        // treinamento, entregas, APR/PT (escrita), NC, risco e plano de ação ainda pendentes.

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        RegistrarEventosNcGQuali();
        AplicarAuditoria();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RegistrarEventosNcGQuali();
        AplicarAuditoria();
        return base.SaveChangesAsync(cancellationToken);
    }

    public void DescartarAlteracoesPendentes() => ChangeTracker.Clear();

    public bool EscopoObraGlobal => _usuarioAtual.TemAcessoGlobal;
    public IReadOnlyList<Guid> ObrasNoEscopo => _usuarioAtual.ObrasPermitidas;

    // Integração com o G-QUALI: toda mudança de NC com obra vira um evento na fila de saída, na MESMA gravação
    // (se a NC não for salva, o evento também não é). Exclusão (física ou lógica) = cancelada.
    private void RegistrarEventosNcGQuali()
    {
        ChangeTracker.DetectChanges();
        var mudancas = ChangeTracker.Entries<NaoConformidade>()
            .Where(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) && e.Entity.ObraId != null)
            .ToList();
        if (mudancas.Count == 0) return;
        var obras = mudancas.Select(e => e.Entity.ObraId!.Value).Distinct().ToList();
        var codigos = Set<Obra>().IgnoreQueryFilters().AsNoTracking()
            .Where(o => obras.Contains(o.Id)).ToDictionary(o => o.Id, o => o.Codigo);
        var agora = DateTime.UtcNow;
        foreach (var e in mudancas)
        {
            if (!codigos.TryGetValue(e.Entity.ObraId!.Value, out var codigo) || string.IsNullOrWhiteSpace(codigo)) continue;
            var tipo = e.State switch
            {
                EntityState.Added => EventoNcGQualiFactory.Criada,
                EntityState.Deleted => EventoNcGQualiFactory.Cancelada,
                _ when !e.Entity.Ativo => EventoNcGQualiFactory.Cancelada,
                _ when e.Entity.Status == StatusNaoConformidade.Encerrada
                    && e.Property(n => n.Status).OriginalValue != StatusNaoConformidade.Encerrada => EventoNcGQualiFactory.Encerrada,
                _ => EventoNcGQualiFactory.Atualizada,
            };
            EventosNcGQuali.Add(EventoNcGQualiFactory.Criar(e.Entity, codigo, tipo, agora));
        }
    }

    private void AplicarAuditoria()
    {
        var agora = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = agora;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = agora;
                    break;
                case EntityState.Deleted:
                    // Nenhum registro crítico é excluído fisicamente — converter em soft-delete.
                    entry.State = EntityState.Modified;
                    entry.Entity.Ativo = false;
                    entry.Entity.UpdatedAtUtc = agora;
                    break;
            }
        }

        // LGPD: CpfHash é derivado do Cpf em texto plano (aqui ainda não criptografado — o
        // ValueConverter só age na fronteira com o banco) e nunca definido manualmente pela aplicação.
        foreach (var entry in ChangeTracker.Entries<Trabalhador>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified && !string.IsNullOrEmpty(entry.Entity.Cpf))
                entry.Entity.CpfHash = CpfCriptografiaConversor.CalcularHash(entry.Entity.Cpf);
        }
    }
}
