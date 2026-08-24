using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AtividadeConfiguracao : IEntityTypeConfiguration<Atividade>
{
    public void Configure(EntityTypeBuilder<Atividade> builder)
    {
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(200);
        builder.HasOne(a => a.Obra).WithMany(o => o.Atividades)
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.ObraId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class PerigoConfiguracao : IEntityTypeConfiguration<Perigo>
{
    public void Configure(EntityTypeBuilder<Perigo> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class RiscoConfiguracao : IEntityTypeConfiguration<Risco>
{
    public void Configure(EntityTypeBuilder<Risco> builder)
    {
        builder.HasOne(r => r.Atividade).WithMany(a => a.Riscos)
            .HasForeignKey(r => r.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Perigo).WithMany(p => p.Riscos)
            .HasForeignKey(r => r.PerigoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.AtividadeId, r.NivelRisco });
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class RiscoTrabalhadorExpostoConfiguracao : IEntityTypeConfiguration<RiscoTrabalhadorExposto>
{
    public void Configure(EntityTypeBuilder<RiscoTrabalhadorExposto> builder)
    {
        builder.HasOne(e => e.Risco).WithMany(r => r.TrabalhadoresExpostos)
            .HasForeignKey(e => e.RiscoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.RiscosExpostos)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.RiscoId, e.TrabalhadorId }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class MatrizRiscoConfigConfiguracao : IEntityTypeConfiguration<MatrizRiscoConfig>
{
    public void Configure(EntityTypeBuilder<MatrizRiscoConfig> builder)
    {
        builder.Property(m => m.Nome).IsRequired().HasMaxLength(150);
        builder.HasQueryFilter(m => m.Ativo);
    }
}

public class MatrizRiscoCelulaConfiguracao : IEntityTypeConfiguration<MatrizRiscoCelula>
{
    public void Configure(EntityTypeBuilder<MatrizRiscoCelula> builder)
    {
        builder.HasOne(c => c.MatrizRiscoConfig).WithMany(m => m.Celulas)
            .HasForeignKey(c => c.MatrizRiscoConfigId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.MatrizRiscoConfigId, c.Probabilidade, c.Severidade }).IsUnique();
        builder.HasQueryFilter(c => c.Ativo);
    }
}
