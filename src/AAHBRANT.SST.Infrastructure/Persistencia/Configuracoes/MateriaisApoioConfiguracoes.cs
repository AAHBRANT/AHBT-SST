using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class MaterialApoioConfiguracao : IEntityTypeConfiguration<MaterialApoio>
{
    public void Configure(EntityTypeBuilder<MaterialApoio> builder)
    {
        builder.Property(m => m.Nome).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Categoria).HasMaxLength(100);
        builder.Property(m => m.NomeArquivo).IsRequired().HasMaxLength(260);
        builder.Property(m => m.ContentType).IsRequired().HasMaxLength(150);
        builder.HasIndex(m => m.Categoria);
        builder.HasQueryFilter(m => m.Ativo);
    }
}
