using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class OrdemServicoConfiguracao : IEntityTypeConfiguration<OrdemServico>
{
    public void Configure(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.ToTable("OrdensServico");
        builder.Property(o => o.Numero).IsRequired().HasMaxLength(30);
        builder.Property(o => o.ConteudoJson).IsRequired();
        builder.HasOne(o => o.Trabalhador).WithMany()
            .HasForeignKey(o => o.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Obra>().WithMany()
            .HasForeignKey(o => o.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => o.Numero).IsUnique();
        builder.HasIndex(o => new { o.TrabalhadorId, o.ObraId, o.Revisao }).IsUnique();
    }
}
