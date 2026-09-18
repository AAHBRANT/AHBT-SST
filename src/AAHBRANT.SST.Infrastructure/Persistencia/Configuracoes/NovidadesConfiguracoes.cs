using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class NovidadeVersaoConfiguracao : IEntityTypeConfiguration<NovidadeVersao>
{
    public void Configure(EntityTypeBuilder<NovidadeVersao> builder)
    {
        builder.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Versao).HasMaxLength(40).IsRequired();

        builder.HasMany(n => n.Itens).WithOne(i => i.NovidadeVersao)
            .HasForeignKey(i => i.NovidadeVersaoId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.DataPublicacao);

        // SstDbContext intercepta Remove() e faz soft-delete (Ativo=false) em vez de DELETE real —
        // sem este filtro, "excluir" não escondia a novidade de lugar nenhum (bug encontrado ao
        // testar a tela de cadastro no navegador).
        builder.HasQueryFilter(n => n.Ativo);
    }
}

public class NovidadeVersaoItemConfiguracao : IEntityTypeConfiguration<NovidadeVersaoItem>
{
    public void Configure(EntityTypeBuilder<NovidadeVersaoItem> builder)
    {
        builder.Property(i => i.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Antes).HasMaxLength(1000);
        builder.Property(i => i.Agora).HasMaxLength(1000);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class NovidadeVisualizacaoConfiguracao : IEntityTypeConfiguration<NovidadeVisualizacao>
{
    public void Configure(EntityTypeBuilder<NovidadeVisualizacao> builder)
    {
        builder.HasOne(v => v.NovidadeVersao).WithMany()
            .HasForeignKey(v => v.NovidadeVersaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Usuario).WithMany()
            .HasForeignKey(v => v.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.NovidadeVersaoId, v.UsuarioId }).IsUnique();
        builder.HasQueryFilter(v => v.Ativo);
    }
}
