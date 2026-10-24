using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AcidenteFotoConfiguracao : IEntityTypeConfiguration<AcidenteFoto>
{
    public void Configure(EntityTypeBuilder<AcidenteFoto> builder)
    {
        builder.Property(f => f.FotoContentType).HasMaxLength(100);
        builder.HasOne(f => f.Acidente).WithMany().HasForeignKey(f => f.AcidenteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.AcidenteId, f.Ordem });
        builder.HasQueryFilter(f => f.Ativo);
    }
}
