using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Persistencia;

public class SstDbContext : DbContext, IAppDbContext
{
    public SstDbContext(DbContextOptions<SstDbContext> options) : base(options)
    {
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
    public DbSet<CursoTreinamento> CursosTreinamento => Set<CursoTreinamento>();
    public DbSet<Treinamento> Treinamentos => Set<Treinamento>();
    public DbSet<MatrizTreinamentoFuncao> MatrizTreinamentoFuncoes => Set<MatrizTreinamentoFuncao>();
    public DbSet<CatalogoEpi> CatalogoEpis => Set<CatalogoEpi>();
    public DbSet<EntregaEpi> EntregasEpi => Set<EntregaEpi>();
    public DbSet<MatrizEpiFuncao> MatrizEpiFuncoes => Set<MatrizEpiFuncao>();
    public DbSet<EstoqueEpi> EstoquesEpi => Set<EstoqueEpi>();
    public DbSet<MovimentacaoEstoqueEpi> MovimentacoesEstoqueEpi => Set<MovimentacaoEstoqueEpi>();

    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AlertaHistoricoEnvio> AlertaHistoricoEnvios => Set<AlertaHistoricoEnvio>();
    public DbSet<RegraAlerta> RegrasAlerta => Set<RegraAlerta>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<TrilhaAuditoria> TrilhaAuditoria => Set<TrilhaAuditoria>();

    public DbSet<Atividade> Atividades => Set<Atividade>();
    public DbSet<Perigo> Perigos => Set<Perigo>();
    public DbSet<Risco> Riscos => Set<Risco>();
    public DbSet<RiscoTrabalhadorExposto> RiscoTrabalhadorExpostos => Set<RiscoTrabalhadorExposto>();
    public DbSet<MatrizRiscoConfig> MatrizRiscoConfigs => Set<MatrizRiscoConfig>();
    public DbSet<MatrizRiscoCelula> MatrizRiscoCelulas => Set<MatrizRiscoCelula>();

    public DbSet<Pgr> Pgrs => Set<Pgr>();
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
    public DbSet<PermissaoTrabalhoPerigo> PermissaoTrabalhoPerigos => Set<PermissaoTrabalhoPerigo>();
    public DbSet<PermissaoTrabalhoControle> PermissaoTrabalhoControles => Set<PermissaoTrabalhoControle>();
    public DbSet<PermissaoTrabalhoRequisito> PermissaoTrabalhoRequisitos => Set<PermissaoTrabalhoRequisito>();
    public DbSet<PermissaoTrabalhoResponsavel> PermissaoTrabalhoResponsaveis => Set<PermissaoTrabalhoResponsavel>();

    public DbSet<ChecklistModelo> ChecklistModelos => Set<ChecklistModelo>();
    public DbSet<ChecklistModeloItem> ChecklistModeloItens => Set<ChecklistModeloItem>();
    public DbSet<Inspecao> Inspecoes => Set<Inspecao>();
    public DbSet<InspecaoItemResposta> InspecaoItemRespostas => Set<InspecaoItemResposta>();

    public DbSet<Dds> Dds => Set<Dds>();
    public DbSet<DdsAtividade> DdsAtividades => Set<DdsAtividade>();
    public DbSet<DdsItemChecklist> DdsItensChecklist => Set<DdsItemChecklist>();
    public DbSet<DdsParticipante> DdsParticipantes => Set<DdsParticipante>();
    public DbSet<DdsTelegramEnvio> DdsTelegramEnvios => Set<DdsTelegramEnvio>();

    public DbSet<NaoConformidade> NaoConformidades => Set<NaoConformidade>();
    public DbSet<AcaoPlano> AcoesPlano => Set<AcaoPlano>();

    public DbSet<Acidente> Acidentes => Set<Acidente>();
    public DbSet<RegistroHhtMensal> RegistrosHhtMensais => Set<RegistroHhtMensal>();

    public DbSet<RequisitoLegal> RequisitosLegais => Set<RequisitoLegal>();

    public DbSet<DocumentoGestao> DocumentosGestao => Set<DocumentoGestao>();
    public DbSet<DocumentoRevisao> DocumentoRevisoes => Set<DocumentoRevisao>();


    public DbSet<AtivoSst> AtivosSst => Set<AtivoSst>();

    public DbSet<DocumentoAssinatura> DocumentosAssinatura => Set<DocumentoAssinatura>();
    public DbSet<DocumentoSignatario> DocumentoSignatarios => Set<DocumentoSignatario>();
    public DbSet<CredencialWebAuthn> CredenciaisWebAuthn => Set<CredencialWebAuthn>();
    public DbSet<DispositivoAgenteBiometrico> DispositivosAgenteBiometrico => Set<DispositivoAgenteBiometrico>();
    public DbSet<TemplateBiometricoFutronic> TemplatesBiometricoFutronic => Set<TemplateBiometricoFutronic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SstDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        AplicarAuditoria();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarAuditoria();
        return base.SaveChangesAsync(cancellationToken);
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
