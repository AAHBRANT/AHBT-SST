using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class TemaInspecaoConfiguracao : IEntityTypeConfiguration<TemaInspecao>
{
    public void Configure(EntityTypeBuilder<TemaInspecao> builder)
    {
        builder.Property(t => t.Nome).IsRequired().HasMaxLength(120);
        // Dois temas ativos com o mesmo nome confundiriam os cards; o desativado libera o nome.
        builder.HasIndex(t => t.Nome).IsUnique().HasFilter("[Ativo] = 1");
        builder.HasQueryFilter(t => t.Ativo);
    }
}

// O filtro de query do item (com escopo por obra) fica no SstDbContext, como o de Veículo.
public class ItemInspecionavelConfiguracao : IEntityTypeConfiguration<ItemInspecionavel>
{
    public void Configure(EntityTypeBuilder<ItemInspecionavel> builder)
    {
        builder.Property(i => i.Identificacao).IsRequired().HasMaxLength(150);
        builder.Property(i => i.Descricao).HasMaxLength(500);
        builder.HasOne(i => i.TemaInspecao).WithMany()
            .HasForeignKey(i => i.TemaInspecaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Obra).WithMany()
            .HasForeignKey(i => i.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.TemaInspecaoId, i.ObraId });
        // Mesma identificação duas vezes no mesmo tema e obra seria o mesmo item cadastrado em duplicidade.
        builder.HasIndex(i => new { i.TemaInspecaoId, i.ObraId, i.Identificacao }).IsUnique().HasFilter("[Ativo] = 1");
    }
}
