using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class VeiculoConfiguracao : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.Property(v => v.PlacaPrefixo).IsRequired().HasMaxLength(60);
        builder.Property(v => v.MarcaModelo).HasMaxLength(150);
        builder.Property(v => v.Cor).HasMaxLength(40);
        builder.Property(v => v.Empresa).HasMaxLength(150);
        builder.Property(v => v.Responsavel).HasMaxLength(150);
        builder.HasOne(v => v.Obra).WithMany()
            .HasForeignKey(v => v.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.ObraId, v.Tipo });
        builder.HasQueryFilter(v => v.Ativo);
    }
}
