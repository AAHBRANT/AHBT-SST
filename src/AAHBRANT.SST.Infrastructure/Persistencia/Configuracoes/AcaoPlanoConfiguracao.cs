using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AcaoPlanoConfiguracao : IEntityTypeConfiguration<AcaoPlano>
{
    public void Configure(EntityTypeBuilder<AcaoPlano> builder)
    {
        builder.Property(a => a.OrigemTipo).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Descricao).IsRequired().HasMaxLength(500);

        builder.HasOne(a => a.ResponsavelUsuario).WithMany()
            .HasForeignKey(a => a.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ValidadoPorUsuario).WithMany()
            .HasForeignKey(a => a.ValidadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Índice composto é o principal caminho de acesso: "todas as ações desta NC/Acidente/...".
        builder.HasIndex(a => new { a.OrigemTipo, a.OrigemId });
        builder.HasQueryFilter(a => a.Ativo);
    }
}
