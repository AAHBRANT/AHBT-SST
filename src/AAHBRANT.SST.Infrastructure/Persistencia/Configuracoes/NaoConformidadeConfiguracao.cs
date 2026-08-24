using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class NaoConformidadeConfiguracao : IEntityTypeConfiguration<NaoConformidade>
{
    public void Configure(EntityTypeBuilder<NaoConformidade> builder)
    {
        builder.Property(n => n.Descricao).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.RequisitoRelacionado).HasMaxLength(300);
        builder.Property(n => n.Local).HasMaxLength(200);
        builder.Property(n => n.ObservacoesEncerramento).HasMaxLength(1000);

        builder.HasOne(n => n.Atividade).WithMany()
            .HasForeignKey(n => n.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Risco).WithMany()
            .HasForeignKey(n => n.RiscoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.ResponsavelUsuario).WithMany()
            .HasForeignKey(n => n.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.Status);
        builder.HasQueryFilter(n => n.Ativo);
    }
}
