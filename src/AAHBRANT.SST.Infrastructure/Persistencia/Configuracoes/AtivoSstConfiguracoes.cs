using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AtivoSstConfiguracoes : IEntityTypeConfiguration<AtivoSst>
{
    public void Configure(EntityTypeBuilder<AtivoSst> builder)
    {
        builder.Property(a => a.Identificacao).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Descricao).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Localizacao).HasMaxLength(200);

        builder.HasOne(a => a.Obra)
            .WithMany()
            .HasForeignKey(a => a.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ObraId);
        builder.HasIndex(a => a.TipoAtivo);
        builder.HasQueryFilter(a => a.Ativo);
    }
}
