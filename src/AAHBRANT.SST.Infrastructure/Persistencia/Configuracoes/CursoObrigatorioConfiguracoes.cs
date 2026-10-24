using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RegraCursoObrigatorioConfiguracao : IEntityTypeConfiguration<RegraCursoObrigatorio>
{
    public void Configure(EntityTypeBuilder<RegraCursoObrigatorio> builder)
    {
        builder.Property(r => r.Norma).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Descricao).HasMaxLength(200).IsRequired();
        builder.Property(r => r.PalavrasChave).HasMaxLength(1000);
        builder.HasOne(r => r.CursoTreinamento).WithMany()
            .HasForeignKey(r => r.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(r => r.Ativo);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}

// Query filter com escopo por obra fica no SstDbContext, junto das demais entidades com ObraId.
public class CursoObrigatorioFuncaoObraConfiguracao : IEntityTypeConfiguration<CursoObrigatorioFuncaoObra>
{
    public void Configure(EntityTypeBuilder<CursoObrigatorioFuncaoObra> builder)
    {
        builder.Property(c => c.Motivo).HasMaxLength(500);
        builder.Property(c => c.JustificativaRecusa).HasMaxLength(500);
        builder.Property(c => c.DecididoPorNome).HasMaxLength(200);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Funcao).WithMany()
            .HasForeignKey(c => c.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.CursoTreinamento).WithMany()
            .HasForeignKey(c => c.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ObraId, c.FuncaoId, c.CursoTreinamentoId }).IsUnique();
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}
