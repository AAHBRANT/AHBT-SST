using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class GeracaoPgrEapConfiguracao : IEntityTypeConfiguration<GeracaoPgrEap>
{
    public void Configure(EntityTypeBuilder<GeracaoPgrEap> builder)
    {
        builder.ToTable("GeracoesPgrEap");
        builder.Property(g => g.Nome).IsRequired().HasMaxLength(200);
        builder.Property(g => g.ElaboradoPorNome).HasMaxLength(200);
        builder.Property(g => g.NomeArquivoEap).IsRequired().HasMaxLength(260);
        builder.Property(g => g.OrigemEap).IsRequired().HasMaxLength(20);
        builder.Property(g => g.EstruturaJson).IsRequired();
        builder.Property(g => g.MotivoDevolucao).HasMaxLength(1000);
        builder.Property(g => g.AprovadoPorNome).HasMaxLength(200);
        builder.HasOne(g => g.Obra).WithMany().HasForeignKey(g => g.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Pgr).WithMany().HasForeignKey(g => g.PgrId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.ObraId, g.Status });
        builder.HasQueryFilter(g => g.Ativo);
    }
}
