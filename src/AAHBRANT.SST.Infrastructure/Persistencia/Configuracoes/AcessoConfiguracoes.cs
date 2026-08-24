using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class PerfilAcessoConfiguracao : IEntityTypeConfiguration<PerfilAcesso>
{
    public void Configure(EntityTypeBuilder<PerfilAcesso> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(80);
        // Único por Tipo entre os perfis de sistema; múltiplos perfis customizados (Tipo = null)
        // são permitidos — SQL Server trata cada NULL como distinto num índice único.
        builder.HasIndex(p => p.Tipo).IsUnique();
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PermissaoConfiguracao : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Modulo).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Acao).IsRequired().HasMaxLength(30);
        builder.Property(p => p.Descricao).IsRequired().HasMaxLength(255);
        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PerfilAcessoPermissaoConfiguracao : IEntityTypeConfiguration<PerfilAcessoPermissao>
{
    public void Configure(EntityTypeBuilder<PerfilAcessoPermissao> builder)
    {
        builder.HasOne(p => p.PerfilAcesso).WithMany(pa => pa.Permissoes)
            .HasForeignKey(p => p.PerfilAcessoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Permissao).WithMany()
            .HasForeignKey(p => p.PermissaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.PerfilAcessoId, p.PermissaoId, p.Escopo }).IsUnique();
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.Property(u => u.AzureAdObjectId).IsRequired().HasMaxLength(36);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Nome).IsRequired().HasMaxLength(200);
        builder.HasIndex(u => u.AzureAdObjectId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        // Sem HasDefaultValue, o EF gera DEFAULT 0 na coluna — valor inexistente no enum
        // (StatusUsuario começa em 1=Ativo) — corrigido aqui para refletir o default do CLR.
        builder.Property(u => u.Status).HasDefaultValue(StatusUsuario.Ativo);
        builder.HasOne(u => u.Trabalhador).WithMany()
            .HasForeignKey(u => u.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(u => u.Ativo);
    }
}

public class UsuarioPerfilObraConfiguracao : IEntityTypeConfiguration<UsuarioPerfilObra>
{
    public void Configure(EntityTypeBuilder<UsuarioPerfilObra> builder)
    {
        builder.HasOne(x => x.Usuario).WithMany(u => u.PerfisPorObra)
            .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PerfilAcesso).WithMany()
            .HasForeignKey(x => x.PerfilAcessoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Obra).WithMany()
            .HasForeignKey(x => x.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UsuarioId, x.PerfilAcessoId, x.ObraId }).IsUnique();
        builder.HasQueryFilter(x => x.Ativo);
    }
}
