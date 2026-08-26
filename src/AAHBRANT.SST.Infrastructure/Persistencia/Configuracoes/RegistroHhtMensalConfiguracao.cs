using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RegistroHhtMensalConfiguracao : IEntityTypeConfiguration<RegistroHhtMensal>
{
    public void Configure(EntityTypeBuilder<RegistroHhtMensal> builder)
    {
        builder.HasOne(r => r.Obra).WithMany()
            .HasForeignKey(r => r.ObraId).OnDelete(DeleteBehavior.Restrict);

        // Um único lançamento de HHT por obra/mês — evita duplicidade/soma indevida no cálculo da TG.
        builder.HasIndex(r => new { r.ObraId, r.Ano, r.Mes }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);
    }
}
