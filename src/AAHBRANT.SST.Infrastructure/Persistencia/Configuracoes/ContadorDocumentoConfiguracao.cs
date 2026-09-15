using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ContadorDocumentoConfiguracao : IEntityTypeConfiguration<ContadorDocumento>
{
    public void Configure(EntityTypeBuilder<ContadorDocumento> builder)
    {
        builder.HasIndex(c => new { c.Prefixo, c.Ano }).IsUnique();
    }
}
