using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RegraAlertaConfiguracao : IEntityTypeConfiguration<RegraAlerta>
{
    public void Configure(EntityTypeBuilder<RegraAlerta> builder)
    {
        builder.HasIndex(r => r.Modulo);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(r => r.Ativo);
    }
}
