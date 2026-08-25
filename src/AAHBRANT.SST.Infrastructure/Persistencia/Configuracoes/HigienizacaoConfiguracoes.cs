using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ItemHigienizacaoConfiguracao : IEntityTypeConfiguration<ItemHigienizacao>
{
    public void Configure(EntityTypeBuilder<ItemHigienizacao> builder)
    {
        builder.Property(i => i.Nome).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Local).HasMaxLength(200);

        builder.HasOne(i => i.Obra)
            .WithMany()
            .HasForeignKey(i => i.ObraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ObraId);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class RegistroHigienizacaoConfiguracao : IEntityTypeConfiguration<RegistroHigienizacao>
{
    public void Configure(EntityTypeBuilder<RegistroHigienizacao> builder)
    {
        builder.HasOne(r => r.ItemHigienizacao)
            .WithMany(i => i.Registros)
            .HasForeignKey(r => r.ItemHigienizacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Trabalhador)
            .WithMany()
            .HasForeignKey(r => r.TrabalhadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ItemHigienizacaoId);
        builder.HasQueryFilter(r => r.Ativo);
    }
}
