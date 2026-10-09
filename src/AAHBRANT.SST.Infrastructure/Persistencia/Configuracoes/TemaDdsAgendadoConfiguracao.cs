using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class TemaDdsAgendadoConfiguracao : IEntityTypeConfiguration<TemaDdsAgendado>
{
    public void Configure(EntityTypeBuilder<TemaDdsAgendado> builder)
    {
        builder.ToTable("TemasDdsAgendados");
        builder.Property(t => t.OrigemTipo).IsRequired().HasMaxLength(100);
        builder.Property(t => t.DescricaoOrigem).HasMaxLength(300);

        builder.HasOne(t => t.Obra).WithMany()
            .HasForeignKey(t => t.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CatalogoTemaDds).WithMany()
            .HasForeignKey(t => t.CatalogoTemaDdsId).OnDelete(DeleteBehavior.Restrict);

        // Caminho de acesso: "temas agendados desta obra para este dia" ao abrir o DDS.
        builder.HasIndex(t => new { t.ObraId, t.Data });
    }
}
