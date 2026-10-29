using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class TituloCboConfiguracao : IEntityTypeConfiguration<TituloCbo>
{
    public void Configure(EntityTypeBuilder<TituloCbo> builder)
    {
        builder.ToTable("TitulosCbo");
        builder.Property(t => t.Codigo).HasMaxLength(6).IsRequired();
        builder.Property(t => t.Titulo).HasMaxLength(300).IsRequired();
        builder.HasIndex(t => t.Codigo);
        builder.HasQueryFilter(t => t.Ativo);
    }
}
