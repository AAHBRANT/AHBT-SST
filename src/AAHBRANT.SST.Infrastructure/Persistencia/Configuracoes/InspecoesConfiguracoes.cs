using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ChecklistModeloConfiguracao : IEntityTypeConfiguration<ChecklistModelo>
{
    public void Configure(EntityTypeBuilder<ChecklistModelo> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.HasOne(c => c.ChecklistModeloAnterior).WithMany()
            .HasForeignKey(c => c.ChecklistModeloAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.TipoInspecao);
        builder.HasQueryFilter(c => c.Ativo);
    }
}

public class ChecklistModeloItemConfiguracao : IEntityTypeConfiguration<ChecklistModeloItem>
{
    public void Configure(EntityTypeBuilder<ChecklistModeloItem> builder)
    {
        builder.Property(i => i.Descricao).IsRequired().HasMaxLength(500);
        builder.HasOne(i => i.ChecklistModelo).WithMany(c => c.Itens)
            .HasForeignKey(i => i.ChecklistModeloId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.ChecklistModeloId);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class InspecaoConfiguracao : IEntityTypeConfiguration<Inspecao>
{
    public void Configure(EntityTypeBuilder<Inspecao> builder)
    {
        builder.HasOne(i => i.Obra).WithMany()
            .HasForeignKey(i => i.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Atividade).WithMany()
            .HasForeignKey(i => i.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ChecklistModelo).WithMany()
            .HasForeignKey(i => i.ChecklistModeloId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ResponsavelUsuario).WithMany()
            .HasForeignKey(i => i.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.ObraId);
        builder.HasIndex(i => i.ChecklistModeloId);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class InspecaoItemRespostaConfiguracao : IEntityTypeConfiguration<InspecaoItemResposta>
{
    public void Configure(EntityTypeBuilder<InspecaoItemResposta> builder)
    {
        builder.Property(r => r.Observacao).HasMaxLength(1000);
        builder.HasOne(r => r.Inspecao).WithMany(i => i.Respostas)
            .HasForeignKey(r => r.InspecaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ChecklistModeloItem).WithMany()
            .HasForeignKey(r => r.ChecklistModeloItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.InspecaoId, r.ChecklistModeloItemId });
        builder.HasQueryFilter(r => r.Ativo);
    }
}
