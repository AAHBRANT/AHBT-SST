using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ReuniaoAnaliseOcorrenciaConfiguracao : IEntityTypeConfiguration<ReuniaoAnaliseOcorrencia>
{
    public void Configure(EntityTypeBuilder<ReuniaoAnaliseOcorrencia> builder)
    {
        builder.ToTable("ReunioesAnaliseOcorrencia");
        builder.Property(r => r.Participantes).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.GraphEventId).HasMaxLength(500);
        builder.Property(r => r.LinkTeams).HasMaxLength(2000);
        builder.Property(r => r.MotivoFalha).HasMaxLength(500);

        builder.HasOne(r => r.Acidente).WithMany()
            .HasForeignKey(r => r.AcidenteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AcidenteId);
    }
}
