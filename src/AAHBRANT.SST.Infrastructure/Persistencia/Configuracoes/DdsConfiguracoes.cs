using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class DdsConfiguracao : IEntityTypeConfiguration<Dds>
{
    public void Configure(EntityTypeBuilder<Dds> builder)
    {
        builder.Property(d => d.TopicoPrincipal).IsRequired().HasMaxLength(200);

        builder.HasOne(d => d.Obra)
            .WithMany()
            .HasForeignKey(d => d.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ResponsavelUsuario)
            .WithMany()
            .HasForeignKey(d => d.ResponsavelUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ObraId);
        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class DdsAtividadeConfiguracao : IEntityTypeConfiguration<DdsAtividade>
{
    public void Configure(EntityTypeBuilder<DdsAtividade> builder)
    {
        builder.HasOne(a => a.Dds)
            .WithMany(d => d.Atividades)
            .HasForeignKey(a => a.DdsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Atividade)
            .WithMany()
            .HasForeignKey(a => a.AtividadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.DdsId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class DdsItemChecklistConfiguracao : IEntityTypeConfiguration<DdsItemChecklist>
{
    public void Configure(EntityTypeBuilder<DdsItemChecklist> builder)
    {
        builder.Property(i => i.Descricao).IsRequired().HasMaxLength(500);

        builder.HasOne(i => i.Dds)
            .WithMany(d => d.ItensChecklist)
            .HasForeignKey(i => i.DdsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Risco)
            .WithMany()
            .HasForeignKey(i => i.RiscoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.DdsId);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class DdsParticipanteConfiguracao : IEntityTypeConfiguration<DdsParticipante>
{
    public void Configure(EntityTypeBuilder<DdsParticipante> builder)
    {
        builder.HasOne(p => p.Dds)
            .WithMany(d => d.Participantes)
            .HasForeignKey(p => p.DdsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Trabalhador)
            .WithMany()
            .HasForeignKey(p => p.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.DdsId, p.TrabalhadorId });
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class DdsTelegramEnvioConfiguracao : IEntityTypeConfiguration<DdsTelegramEnvio>
{
    public void Configure(EntityTypeBuilder<DdsTelegramEnvio> builder)
    {
        builder.HasOne(e => e.Dds)
            .WithMany()
            .HasForeignKey(e => e.DdsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Trabalhador)
            .WithMany()
            .HasForeignKey(e => e.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DdsId, e.TrabalhadorId });
        builder.HasQueryFilter(e => e.Ativo);
    }
}
