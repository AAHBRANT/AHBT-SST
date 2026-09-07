using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CatalogoEpcConfiguracao : IEntityTypeConfiguration<CatalogoEpc>
{
    public void Configure(EntityTypeBuilder<CatalogoEpc> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Categoria).HasMaxLength(100);
        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class EstoqueEpcConfiguracao : IEntityTypeConfiguration<EstoqueEpc>
{
    public void Configure(EntityTypeBuilder<EstoqueEpc> builder)
    {
        builder.HasOne(e => e.CatalogoEpc).WithMany(c => c.Estoques)
            .HasForeignKey(e => e.CatalogoEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Obra).WithMany()
            .HasForeignKey(e => e.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CatalogoEpcId, e.ObraId }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class MovimentacaoEstoqueEpcConfiguracao : IEntityTypeConfiguration<MovimentacaoEstoqueEpc>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoqueEpc> builder)
    {
        builder.Property(m => m.Observacao).HasMaxLength(300);
        builder.HasOne(m => m.EstoqueEpc).WithMany(e => e.Movimentacoes)
            .HasForeignKey(m => m.EstoqueEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.EntregaEpc).WithMany()
            .HasForeignKey(m => m.EntregaEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.EstoqueEpcId, m.CreatedAtUtc });
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class MatrizEpcFuncaoConfiguracao : IEntityTypeConfiguration<MatrizEpcFuncao>
{
    public void Configure(EntityTypeBuilder<MatrizEpcFuncao> builder)
    {
        builder.HasOne(m => m.Funcao).WithMany()
            .HasForeignKey(m => m.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.CatalogoEpc).WithMany()
            .HasForeignKey(m => m.CatalogoEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FuncaoId, m.CatalogoEpcId }).IsUnique();
        builder.HasQueryFilter(m => m.Ativo);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}

public class EntregaEpcConfiguracao : IEntityTypeConfiguration<EntregaEpc>
{
    public void Configure(EntityTypeBuilder<EntregaEpc> builder)
    {
        builder.Property(e => e.Observacoes).HasMaxLength(300);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.EntregasEpc)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CatalogoEpc).WithMany(c => c.Entregas)
            .HasForeignKey(e => e.CatalogoEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TrabalhadorId, e.DataEntrega });
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
