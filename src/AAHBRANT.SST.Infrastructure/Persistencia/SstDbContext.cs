using AAHBRANT.SST.Application.Common.Interfaces;
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
    public DbSet<CursoTreinamento> CursosTreinamento => Set<CursoTreinamento>();
    public DbSet<Treinamento> Treinamentos => Set<Treinamento>();
    public DbSet<CatalogoEpi> CatalogoEpis => Set<CatalogoEpi>();
    public DbSet<EntregaEpi> EntregasEpi => Set<EntregaEpi>();

    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AlertaHistoricoEnvio> AlertaHistoricoEnvios => Set<AlertaHistoricoEnvio>();
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

    public DbSet<RequisitoLegal> RequisitosLegais => Set<RequisitoLegal>();

    public DbSet<DocumentoGestao> DocumentosGestao => Set<DocumentoGestao>();
    public DbSet<DocumentoRevisao> DocumentoRevisoes => Set<DocumentoRevisao>();

    public DbSet<IdempotenciaRegistro> IdempotenciaRegistros => Set<IdempotenciaRegistro>();

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

        // Camada 3 do RBAC (docs/RBAC-Matrix.md §4, "Global Query Filter... mitiga BOLA"): as 8
        // entidades abaixo têm ObraId direto na própria tabela — são o alvo do filtro. Cada uma
        // SUBSTITUI (não acumula com) o HasQueryFilter(x => x.Ativo) já registrado por sua própria
        // Configuracao logo acima (EF Core só guarda um filtro por entidade) — por isso a condição
        // Ativo é repetida aqui explicitamente, senão o soft-delete deixaria de funcionar para
        // estas 8 entidades. Sem efeito hoje (TemAcessoGlobal fica true enquanto a autenticação
        // Entra ID não estiver configurada — ver EscopoPorObraMiddleware): a consulta gerada é
        // idêntica à de antes até a autenticação real entrar em vigor.
        modelBuilder.Entity<Dds>().HasQueryFilter(d =>
            d.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(d.ObraId)));
        modelBuilder.Entity<Inspecao>().HasQueryFilter(i =>
            i.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(i.ObraId)));
        modelBuilder.Entity<Acidente>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<Pgr>().HasQueryFilter(p =>
            p.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(p.ObraId)));
        modelBuilder.Entity<Atividade>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));
        modelBuilder.Entity<Setor>().HasQueryFilter(s =>
            s.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(s.ObraId)));
        modelBuilder.Entity<Trabalhador>().HasQueryFilter(t =>
            t.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(t.ObraId)));
        modelBuilder.Entity<AreaSst>().HasQueryFilter(a =>
            a.Ativo && (_usuarioAtual.TemAcessoGlobal || _usuarioAtual.ObrasPermitidas.Contains(a.ObraId)));

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
