using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class DocumentoGestaoConfiguracao : IEntityTypeConfiguration<DocumentoGestao>
{
    public void Configure(EntityTypeBuilder<DocumentoGestao> builder)
    {
        builder.Property(d => d.Nome).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Tipo).HasMaxLength(100);
        builder.Property(d => d.Categoria).HasMaxLength(100);
        builder.Property(d => d.OrigemDocumento).HasMaxLength(200);
        builder.Property(d => d.Versao).HasMaxLength(50);
        builder.Property(d => d.Arquivo).HasMaxLength(500);

        builder.HasOne(d => d.ResponsavelUsuario).WithMany()
            .HasForeignKey(d => d.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.RequisitoLegal).WithMany()
            .HasForeignKey(d => d.RequisitoLegalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Obra).WithMany()
            .HasForeignKey(d => d.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Setor).WithMany()
            .HasForeignKey(d => d.SetorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Revisoes).WithOne(r => r.Documento)
            .HasForeignKey(r => r.DocumentoId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.Tipo);
        builder.HasIndex(d => d.Categoria);
        builder.HasQueryFilter(d => d.Ativo);

        // Faltava aqui (única configuração sem essa linha, ao contrário de todas as demais
        // entidades do projeto) — causava "Cannot insert an explicit value into a timestamp
        // column" ao criar PCMSO, pois a coluna física já é rowversion mas o EF a tratava
        // como varbinary(max) comum e tentava inserir NULL explicitamente nela.
        builder.Property(d => d.RowVersion).IsRowVersion();
    }
}

public class DocumentoRevisaoConfiguracao : IEntityTypeConfiguration<DocumentoRevisao>
{
    public void Configure(EntityTypeBuilder<DocumentoRevisao> builder)
    {
        builder.Property(r => r.Motivo).IsRequired().HasMaxLength(1000);

        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => r.Ativo);
    }
}
