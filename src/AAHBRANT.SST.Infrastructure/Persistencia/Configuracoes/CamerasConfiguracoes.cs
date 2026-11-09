using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CameraConfiguracao : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> builder)
    {
        builder.ToTable("Cameras");
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AreaSst).WithMany()
            .HasForeignKey(c => c.AreaSstId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ObraId, c.Nome });
    }
}

public class OcorrenciaEpiCameraConfiguracao : IEntityTypeConfiguration<OcorrenciaEpiCamera>
{
    public void Configure(EntityTypeBuilder<OcorrenciaEpiCamera> builder)
    {
        builder.ToTable("OcorrenciasEpiCamera");
        builder.Property(o => o.Foto).IsRequired();
        builder.HasOne(o => o.Camera).WithMany()
            .HasForeignKey(o => o.CameraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Trabalhador).WithMany()
            .HasForeignKey(o => o.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.ObraId, o.RecebidaEmUtc });
        builder.HasIndex(o => new { o.CameraId, o.RecebidaEmUtc });
    }
}
