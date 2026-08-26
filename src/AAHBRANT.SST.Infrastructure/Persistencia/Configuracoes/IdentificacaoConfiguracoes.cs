using System.Text.Json;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class TagIdentificacaoConfiguracao : IEntityTypeConfiguration<TagIdentificacao>
{
    public void Configure(EntityTypeBuilder<TagIdentificacao> builder)
    {
        builder.Property(t => t.Uid).IsRequired().HasMaxLength(100);
        builder.HasIndex(t => t.Uid).IsUnique();
        builder.HasIndex(t => new { t.EntidadeVinculadaTipo, t.EntidadeVinculadaId });
        builder.HasQueryFilter(t => t.Ativo);
    }
}

public class AreaSstConfiguracao : IEntityTypeConfiguration<AreaSst>
{
    public void Configure(EntityTypeBuilder<AreaSst> builder)
    {
        builder.Property(a => a.Codigo).IsRequired().HasMaxLength(50);
        builder.HasIndex(a => a.Codigo).IsUnique();
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(150);
        builder.Property(a => a.DetalhesLocalizacao).HasMaxLength(255);

        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.ObraId);

        var conversorLista = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var comparadorLista = new ValueComparer<List<string>>(
            (l1, l2) => (l1 ?? new List<string>()).SequenceEqual(l2 ?? new List<string>()),
            l => l.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            l => l.ToList());

        builder.Property(a => a.Riscos)
            .HasConversion(conversorLista, comparadorLista)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.Requisitos)
            .HasConversion(conversorLista, comparadorLista)
            .HasColumnType("nvarchar(max)");

        builder.HasQueryFilter(a => a.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
