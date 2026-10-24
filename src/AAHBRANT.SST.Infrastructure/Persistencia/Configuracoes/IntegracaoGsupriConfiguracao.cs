using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class GsupriObraConfiguracao : IEntityTypeConfiguration<GsupriObra>
{
    public void Configure(EntityTypeBuilder<GsupriObra> b)
    {
        b.ToTable("GsupriObras"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoExterno).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.CodigoExterno).IsUnique();
        b.HasOne<Obra>().WithMany().HasForeignKey(x => x.ObraId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class GsupriProdutoConfiguracao : IEntityTypeConfiguration<GsupriProduto>
{
    public void Configure(EntityTypeBuilder<GsupriProduto> b)
    {
        b.ToTable("GsupriProdutos"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoExterno).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Unidade).HasMaxLength(10);
        b.Property(x => x.Categoria).HasMaxLength(10);
        b.Property(x => x.Tamanho).HasMaxLength(20);
        b.Property(x => x.FatorConversao).HasPrecision(18, 6);
        b.HasIndex(x => new { x.CodigoExterno, x.Unidade }).IsUnique();
    }
}
public class GsupriRecebimentoConfiguracao : IEntityTypeConfiguration<GsupriRecebimento>
{
    public void Configure(EntityTypeBuilder<GsupriRecebimento> b)
    {
        b.ToTable("GsupriRecebimentos"); b.HasKey(x => x.Id);
        b.Property(x => x.RecebimentoExternoId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.ObraCodigo).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Hash).HasMaxLength(64);
        b.Property(x => x.Status).HasMaxLength(40);
        b.Property(x => x.Pendencia).HasMaxLength(1000);
        b.HasIndex(x => x.RecebimentoExternoId).IsUnique();
        b.HasOne<Obra>().WithMany().HasForeignKey(x => x.ObraId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne(x => x.Recebimento).HasForeignKey(x => x.RecebimentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class GsupriItemConfiguracao : IEntityTypeConfiguration<GsupriRecebimentoItem>
{
    public void Configure(EntityTypeBuilder<GsupriRecebimentoItem> b)
    {
        b.ToTable("GsupriRecebimentoItens"); b.HasKey(x => x.Id);
        b.Property(x => x.ItemExternoId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.ProdutoCodigo).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Unidade).HasMaxLength(10);
        b.Property(x => x.Pendencia).HasMaxLength(1000);
        b.HasIndex(x => new { x.RecebimentoId, x.ItemExternoId }).IsUnique();
        b.HasOne(x => x.ProdutoVinculo).WithMany().HasForeignKey(x => x.ProdutoVinculoId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class GsupriEventoConfiguracao : IEntityTypeConfiguration<GsupriEvento>
{
    public void Configure(EntityTypeBuilder<GsupriEvento> b)
    {
        b.ToTable("GsupriEventos"); b.HasKey(x => x.Id);
        b.Property(x => x.EventoExternoId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Hash).HasMaxLength(64);
        b.HasIndex(x => x.EventoExternoId).IsUnique();
        b.HasOne<GsupriRecebimento>().WithMany().HasForeignKey(x => x.RecebimentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
