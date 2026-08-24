using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class PermissaoTrabalhoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalho>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalho> builder)
    {
        builder.Property(p => p.Local).IsRequired().HasMaxLength(200);
        builder.Property(p => p.ObservacoesEncerramento).HasMaxLength(500);
        builder.HasOne(p => p.Atividade).WithMany()
            .HasForeignKey(p => p.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Equipe).WithMany()
            .HasForeignKey(p => p.EquipeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AutorizadoPorUsuario).WithMany()
            .HasForeignKey(p => p.AutorizadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.EncerradaPorUsuario).WithMany()
            .HasForeignKey(p => p.EncerradaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.AtividadeId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PermissaoTrabalhoPerigoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoPerigo>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoPerigo> builder)
    {
        builder.HasOne(pp => pp.PermissaoTrabalho).WithMany(p => p.Perigos)
            .HasForeignKey(pp => pp.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(pp => pp.Perigo).WithMany()
            .HasForeignKey(pp => pp.PerigoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(pp => new { pp.PermissaoTrabalhoId, pp.PerigoId });
        builder.HasQueryFilter(pp => pp.Ativo);
    }
}

public class PermissaoTrabalhoControleConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoControle>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoControle> builder)
    {
        builder.Property(c => c.Descricao).IsRequired().HasMaxLength(500);
        builder.HasOne(c => c.PermissaoTrabalho).WithMany(p => p.Controles)
            .HasForeignKey(c => c.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.PermissaoTrabalhoId);
        builder.HasQueryFilter(c => c.Ativo);
    }
}

public class PermissaoTrabalhoRequisitoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoRequisito>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoRequisito> builder)
    {
        builder.Property(r => r.Descricao).IsRequired().HasMaxLength(500);
        builder.HasOne(r => r.PermissaoTrabalho).WithMany(p => p.Requisitos)
            .HasForeignKey(r => r.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.PermissaoTrabalhoId);
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class PermissaoTrabalhoResponsavelConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoResponsavel>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoResponsavel> builder)
    {
        builder.HasOne(r => r.PermissaoTrabalho).WithMany(p => p.Responsaveis)
            .HasForeignKey(r => r.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Trabalhador).WithMany()
            .HasForeignKey(r => r.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.PermissaoTrabalhoId, r.TrabalhadorId });
        builder.HasQueryFilter(r => r.Ativo);
    }
}
