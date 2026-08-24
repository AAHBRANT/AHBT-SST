using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class PgrConfiguracao : IEntityTypeConfiguration<Pgr>
{
    public void Configure(EntityTypeBuilder<Pgr> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.HasOne(p => p.Obra).WithMany()
            .HasForeignKey(p => p.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ResponsavelUsuario).WithMany()
            .HasForeignKey(p => p.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.ObraId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PlanoAcaoItemConfiguracao : IEntityTypeConfiguration<PlanoAcaoItem>
{
    public void Configure(EntityTypeBuilder<PlanoAcaoItem> builder)
    {
        builder.Property(i => i.Descricao).IsRequired().HasMaxLength(500);
        builder.HasOne(i => i.Pgr).WithMany(p => p.PlanoDeAcao)
            .HasForeignKey(i => i.PgrId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Risco).WithMany()
            .HasForeignKey(i => i.RiscoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ResponsavelUsuario).WithMany()
            .HasForeignKey(i => i.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.PgrId, i.Status });
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class PgrRevisaoConfiguracao : IEntityTypeConfiguration<PgrRevisao>
{
    public void Configure(EntityTypeBuilder<PgrRevisao> builder)
    {
        builder.Property(r => r.Motivo).IsRequired().HasMaxLength(500);
        builder.HasOne(r => r.Pgr).WithMany(p => p.Revisoes)
            .HasForeignKey(r => r.PgrId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.PgrId, r.NumeroRevisao }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);
    }
}
