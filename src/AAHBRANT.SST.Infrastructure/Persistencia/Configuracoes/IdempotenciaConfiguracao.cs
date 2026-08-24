using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class IdempotenciaConfiguracao : IEntityTypeConfiguration<IdempotenciaRegistro>
{
    public void Configure(EntityTypeBuilder<IdempotenciaRegistro> builder)
    {
        builder.Property(i => i.Chave).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Rota).IsRequired().HasMaxLength(500);
        builder.Property(i => i.CorpoResposta).IsRequired();

        builder.HasIndex(i => i.Chave).IsUnique();
    }
}
