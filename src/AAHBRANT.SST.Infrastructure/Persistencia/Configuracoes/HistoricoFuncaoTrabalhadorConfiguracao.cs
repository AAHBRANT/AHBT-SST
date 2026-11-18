using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class HistoricoFuncaoTrabalhadorConfiguracao : IEntityTypeConfiguration<HistoricoFuncaoTrabalhador>
{
    public void Configure(EntityTypeBuilder<HistoricoFuncaoTrabalhador> builder)
    {
        builder.ToTable("HistoricosFuncaoTrabalhador");

        builder.HasOne(h => h.Trabalhador).WithMany()
            .HasForeignKey(h => h.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.FuncaoAnterior).WithMany()
            .HasForeignKey(h => h.FuncaoAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.FuncaoNova).WithMany()
            .HasForeignKey(h => h.FuncaoNovaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.TrabalhadorId, h.AlteradaEm });
        builder.HasQueryFilter(h => h.Ativo);
    }
}
