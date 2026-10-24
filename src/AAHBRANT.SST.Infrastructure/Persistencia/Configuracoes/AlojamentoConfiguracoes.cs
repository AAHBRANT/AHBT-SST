using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AlojamentoConfiguracao : IEntityTypeConfiguration<Alojamento>
{
    public void Configure(EntityTypeBuilder<Alojamento> builder)
    {
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Endereco).HasMaxLength(300);
        builder.Property(a => a.GrhAlojamentoId).HasMaxLength(100);
        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.GrhAlojamentoId).IsUnique().HasFilter("[GrhAlojamentoId] IS NOT NULL");
        builder.HasQueryFilter(a => a.Ativo);
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}

public class AlojamentoMoradorConfiguracao : IEntityTypeConfiguration<AlojamentoMorador>
{
    public void Configure(EntityTypeBuilder<AlojamentoMorador> builder)
    {
        builder.HasOne(m => m.Alojamento).WithMany(a => a.Moradores)
            .HasForeignKey(m => m.AlojamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Trabalhador).WithMany()
            .HasForeignKey(m => m.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        // Um trabalhador só pode ter um vínculo ativo (DataSaida nula) por vez — em qualquer alojamento.
        builder.HasIndex(m => m.TrabalhadorId).IsUnique().HasFilter("[DataSaida] IS NULL");
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class ConfiguracaoAlojamentoConfiguracao : IEntityTypeConfiguration<ConfiguracaoAlojamento>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoAlojamento> builder)
    {
        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}
