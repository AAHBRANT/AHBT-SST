using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CatalogoUniformeConfiguracao : IEntityTypeConfiguration<CatalogoUniforme>
{
    public void Configure(EntityTypeBuilder<CatalogoUniforme> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Categoria).HasMaxLength(100);
        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class EstoqueUniformeConfiguracao : IEntityTypeConfiguration<EstoqueUniforme>
{
    public void Configure(EntityTypeBuilder<EstoqueUniforme> builder)
    {
        builder.Property(e => e.Tamanho).IsRequired().HasMaxLength(20);
        builder.HasOne(e => e.CatalogoUniforme).WithMany(c => c.Estoques)
            .HasForeignKey(e => e.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Obra).WithMany()
            .HasForeignKey(e => e.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CatalogoUniformeId, e.ObraId, e.Tamanho }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class MovimentacaoEstoqueUniformeConfiguracao : IEntityTypeConfiguration<MovimentacaoEstoqueUniforme>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoqueUniforme> builder)
    {
        builder.Property(m => m.Observacao).HasMaxLength(300);
        builder.HasOne(m => m.EstoqueUniforme).WithMany(e => e.Movimentacoes)
            .HasForeignKey(m => m.EstoqueUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.EntregaUniforme).WithMany()
            .HasForeignKey(m => m.EntregaUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.EstoqueUniformeId, m.CreatedAtUtc });
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class MatrizUniformeFuncaoConfiguracao : IEntityTypeConfiguration<MatrizUniformeFuncao>
{
    public void Configure(EntityTypeBuilder<MatrizUniformeFuncao> builder)
    {
        builder.HasOne(m => m.Funcao).WithMany()
            .HasForeignKey(m => m.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.CatalogoUniforme).WithMany()
            .HasForeignKey(m => m.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FuncaoId, m.CatalogoUniformeId }).IsUnique();
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class TrabalhadorTamanhoUniformeConfiguracao : IEntityTypeConfiguration<TrabalhadorTamanhoUniforme>
{
    public void Configure(EntityTypeBuilder<TrabalhadorTamanhoUniforme> builder)
    {
        builder.Property(t => t.Tamanho).IsRequired().HasMaxLength(20);
        builder.HasOne(t => t.Trabalhador).WithMany(t => t.TamanhosUniforme)
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CatalogoUniforme).WithMany()
            .HasForeignKey(t => t.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.TrabalhadorId, t.CatalogoUniformeId }).IsUnique();
        builder.HasQueryFilter(t => t.Ativo);
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}

public class EntregaUniformeConfiguracao : IEntityTypeConfiguration<EntregaUniforme>
{
    public void Configure(EntityTypeBuilder<EntregaUniforme> builder)
    {
        builder.Property(e => e.Tamanho).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Observacoes).HasMaxLength(300);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.EntregasUniforme)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CatalogoUniforme).WithMany(c => c.Entregas)
            .HasForeignKey(e => e.CatalogoUniformeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TrabalhadorId, e.DataEntrega });
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
