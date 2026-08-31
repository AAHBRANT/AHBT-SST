using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AprConfiguracao : IEntityTypeConfiguration<Apr>
{
    public void Configure(EntityTypeBuilder<Apr> builder)
    {
        builder.Property(a => a.NumeroApr).HasMaxLength(60);
        builder.Property(a => a.Local).IsRequired().HasMaxLength(200);
        builder.Property(a => a.MaquinasEquipamentos).HasMaxLength(500);
        builder.Property(a => a.PgrReferencia).HasMaxLength(300);
        builder.Property(a => a.MotivoReprovacao).HasMaxLength(500);
        builder.HasOne(a => a.Atividade).WithMany()
            .HasForeignKey(a => a.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Equipe).WithMany()
            .HasForeignKey(a => a.EquipeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AprovadoPorUsuario).WithMany()
            .HasForeignKey(a => a.AprovadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.AtividadeId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class AprEtapaConfiguracao : IEntityTypeConfiguration<AprEtapa>
{
    public void Configure(EntityTypeBuilder<AprEtapa> builder)
    {
        builder.Property(e => e.Descricao).IsRequired().HasMaxLength(500);
        builder.HasOne(e => e.Apr).WithMany(a => a.Etapas)
            .HasForeignKey(e => e.AprId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.AprId, e.Ordem });
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class AprEtapaRiscoConfiguracao : IEntityTypeConfiguration<AprEtapaRisco>
{
    public void Configure(EntityTypeBuilder<AprEtapaRisco> builder)
    {
        builder.Property(er => er.PerigoEventoPerigoso).IsRequired().HasMaxLength(300);
        builder.Property(er => er.FonteCircunstancia).HasMaxLength(500);
        builder.Property(er => er.PossiveisLesoes).HasMaxLength(500);
        builder.Property(er => er.TrabalhadoresExpostos).HasMaxLength(300);
        builder.Property(er => er.MedidasPrevencao).HasMaxLength(1000);
        builder.Property(er => er.Responsavel).HasMaxLength(200);
        builder.HasOne(er => er.AprEtapa).WithMany(e => e.Riscos)
            .HasForeignKey(er => er.AprEtapaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(er => er.AprEtapaId);
        builder.HasQueryFilter(er => er.Ativo);
    }
}

public class AprResponsavelConfiguracao : IEntityTypeConfiguration<AprResponsavel>
{
    public void Configure(EntityTypeBuilder<AprResponsavel> builder)
    {
        builder.HasOne(r => r.Apr).WithMany(a => a.Responsaveis)
            .HasForeignKey(r => r.AprId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Trabalhador).WithMany()
            .HasForeignKey(r => r.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.AprId, r.TrabalhadorId });
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class AprAssinaturaConfiguracao : IEntityTypeConfiguration<AprAssinatura>
{
    public void Configure(EntityTypeBuilder<AprAssinatura> builder)
    {
        builder.HasOne(s => s.Apr).WithMany(a => a.Assinaturas)
            .HasForeignKey(s => s.AprId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Trabalhador).WithMany()
            .HasForeignKey(s => s.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.AprId, s.TrabalhadorId, s.Papel }).IsUnique();
        builder.HasQueryFilter(s => s.Ativo);
    }
}
