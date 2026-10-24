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
        builder.Property(a => a.Fundamentacao).HasMaxLength(500);

        builder.HasOne(a => a.ResponsavelUsuario).WithMany()
            .HasForeignKey(a => a.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ValidadoPorUsuario).WithMany()
            .HasForeignKey(a => a.ValidadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ConcluidoPorUsuario).WithMany()
            .HasForeignKey(a => a.ConcluidoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.DescricaoConclusao).HasMaxLength(1000);
        builder.HasOne(a => a.ResolucaoCursoTreinamento).WithMany()
            .HasForeignKey(a => a.ResolucaoCursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ResolucaoTemaInspecao).WithMany()
            .HasForeignKey(a => a.ResolucaoTemaInspecaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ResolucaoCatalogoEpi).WithMany()
            .HasForeignKey(a => a.ResolucaoCatalogoEpiId).OnDelete(DeleteBehavior.Restrict);

        // Índice composto é o principal caminho de acesso: "todas as ações desta NC/Acidente/...".
        builder.HasIndex(a => new { a.OrigemTipo, a.OrigemId });
        builder.HasQueryFilter(a => a.Ativo);

        // Faltava aqui, mesmo bug já corrigido em DocumentoGestaoConfiguracao: "Cannot insert an
        // explicit value into a timestamp column" ao criar Ação do Plano.
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
