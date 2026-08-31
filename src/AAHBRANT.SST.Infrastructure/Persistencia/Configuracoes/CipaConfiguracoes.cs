using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class DimensionamentoCipaConfiguracao : IEntityTypeConfiguration<DimensionamentoCipa>
{
    public void Configure(EntityTypeBuilder<DimensionamentoCipa> builder)
    {
        builder.Property(d => d.Cnae).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Observacoes).HasMaxLength(1000);

        builder.HasOne(d => d.Obra)
            .WithMany()
            .HasForeignKey(d => d.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ObraId);
        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class ProcessoEleitoralCipaConfiguracao : IEntityTypeConfiguration<ProcessoEleitoralCipa>
{
    public void Configure(EntityTypeBuilder<ProcessoEleitoralCipa> builder)
    {
        builder.Property(p => p.NumeroDocumento).HasMaxLength(50);

        builder.HasOne(p => p.Obra)
            .WithMany()
            .HasForeignKey(p => p.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ObraId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class CandidatoCipaConfiguracao : IEntityTypeConfiguration<CandidatoCipa>
{
    public void Configure(EntityTypeBuilder<CandidatoCipa> builder)
    {
        builder.Property(c => c.MotivoIndeferimento).HasMaxLength(500);

        builder.HasOne(c => c.ProcessoEleitoral)
            .WithMany(p => p.Candidatos)
            .HasForeignKey(c => c.ProcessoEleitoralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Trabalhador)
            .WithMany()
            .HasForeignKey(c => c.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ProcessoEleitoralId);
        builder.HasIndex(c => new { c.ProcessoEleitoralId, c.TrabalhadorId });
        builder.HasQueryFilter(c => c.Ativo);
    }
}

public class MembroCipaConfiguracao : IEntityTypeConfiguration<MembroCipa>
{
    public void Configure(EntityTypeBuilder<MembroCipa> builder)
    {
        builder.HasOne(m => m.Obra)
            .WithMany()
            .HasForeignKey(m => m.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Trabalhador)
            .WithMany()
            .HasForeignKey(m => m.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ProcessoEleitoral)
            .WithMany()
            .HasForeignKey(m => m.ProcessoEleitoralId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CandidatoCipa)
            .WithMany()
            .HasForeignKey(m => m.CandidatoCipaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ObraId);
        builder.HasIndex(m => m.TrabalhadorId);
        builder.HasQueryFilter(m => m.Ativo);
    }
}

public class TreinamentoCipaConfiguracao : IEntityTypeConfiguration<TreinamentoCipa>
{
    public void Configure(EntityTypeBuilder<TreinamentoCipa> builder)
    {
        builder.Property(t => t.ConteudoProgramatico).HasMaxLength(2000);
        builder.Property(t => t.InstituicaoInstrutor).HasMaxLength(200);
        builder.Property(t => t.CertificadoContentType).HasMaxLength(100);
        builder.Property(t => t.ListaPresencaContentType).HasMaxLength(100);

        builder.HasOne(t => t.MembroCipa)
            .WithMany(m => m.Treinamentos)
            .HasForeignKey(t => t.MembroCipaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.MembroCipaId);
        builder.HasQueryFilter(t => t.Ativo);
    }
}

public class ReuniaoCipaConfiguracao : IEntityTypeConfiguration<ReuniaoCipa>
{
    public void Configure(EntityTypeBuilder<ReuniaoCipa> builder)
    {
        builder.Property(r => r.Pauta).HasMaxLength(2000);
        builder.Property(r => r.Deliberacoes).HasMaxLength(4000);

        builder.HasOne(r => r.Obra)
            .WithMany()
            .HasForeignKey(r => r.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ObraId);
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class ParticipanteReuniaoCipaConfiguracao : IEntityTypeConfiguration<ParticipanteReuniaoCipa>
{
    public void Configure(EntityTypeBuilder<ParticipanteReuniaoCipa> builder)
    {
        builder.HasOne(p => p.ReuniaoCipa)
            .WithMany(r => r.Participantes)
            .HasForeignKey(p => p.ReuniaoCipaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Trabalhador)
            .WithMany()
            .HasForeignKey(p => p.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ReuniaoCipaId, p.TrabalhadorId });
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class InspecaoCipaConfiguracao : IEntityTypeConfiguration<InspecaoCipa>
{
    public void Configure(EntityTypeBuilder<InspecaoCipa> builder)
    {
        builder.Property(i => i.Local).IsRequired().HasMaxLength(200);
        builder.Property(i => i.RiscoIdentificado).IsRequired().HasMaxLength(1000);

        builder.HasOne(i => i.Obra)
            .WithMany()
            .HasForeignKey(i => i.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.MembroCipa)
            .WithMany()
            .HasForeignKey(i => i.MembroCipaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.NaoConformidade)
            .WithMany()
            .HasForeignKey(i => i.NaoConformidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ObraId);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class EventoSipatConfiguracao : IEntityTypeConfiguration<EventoSipat>
{
    public void Configure(EntityTypeBuilder<EventoSipat> builder)
    {
        builder.Property(e => e.Tema).HasMaxLength(300);
        builder.Property(e => e.Programacao).HasMaxLength(4000);

        builder.HasOne(e => e.Obra)
            .WithMany()
            .HasForeignKey(e => e.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ObraId);
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class AtividadeSipatConfiguracao : IEntityTypeConfiguration<AtividadeSipat>
{
    public void Configure(EntityTypeBuilder<AtividadeSipat> builder)
    {
        builder.Property(a => a.Horario).HasMaxLength(50);
        builder.Property(a => a.TemaPalestra).IsRequired().HasMaxLength(300);
        builder.Property(a => a.Palestrante).HasMaxLength(200);

        builder.HasOne(a => a.EventoSipat)
            .WithMany(e => e.Atividades)
            .HasForeignKey(a => a.EventoSipatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.EventoSipatId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}
