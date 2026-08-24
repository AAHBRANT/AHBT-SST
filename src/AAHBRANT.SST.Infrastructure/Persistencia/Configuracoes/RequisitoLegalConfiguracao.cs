using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RequisitoLegalConfiguracao : IEntityTypeConfiguration<RequisitoLegal>
{
    public void Configure(EntityTypeBuilder<RequisitoLegal> builder)
    {
        builder.Property(r => r.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Norma).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Item).HasMaxLength(100);
        builder.Property(r => r.Tema).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Requisito).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.Justificativa).HasMaxLength(1000);
        builder.Property(r => r.Evidencia).HasMaxLength(500);
        builder.Property(r => r.Periodicidade).HasMaxLength(100);

        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Obra).WithMany()
            .HasForeignKey(r => r.ObraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Norma);
        builder.HasQueryFilter(r => r.Ativo);
    }
}
