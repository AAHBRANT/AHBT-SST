using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class PermissaoTrabalhoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalho>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalho> builder)
    {
        builder.Property(p => p.NumeroPt).HasMaxLength(60);
        builder.Property(p => p.DescricaoAtividade).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Local).IsRequired().HasMaxLength(200);
        builder.Property(p => p.EmpresaExecutante).HasMaxLength(200);
        builder.Property(p => p.MotivoSuspensao).HasMaxLength(500);
        builder.Property(p => p.ObservacoesEncerramento).HasMaxLength(500);
        builder.Property(p => p.OutrosEpis).HasMaxLength(300);
        builder.Property(p => p.OutrosEpcs).HasMaxLength(300);

        builder.HasOne(p => p.Atividade).WithMany()
            .HasForeignKey(p => p.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Equipe).WithMany()
            .HasForeignKey(p => p.EquipeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ResponsavelExecucaoUsuario).WithMany()
            .HasForeignKey(p => p.ResponsavelExecucaoUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ResponsavelAreaUsuario).WithMany()
            .HasForeignKey(p => p.ResponsavelAreaUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AutorizadoPorUsuario).WithMany()
            .HasForeignKey(p => p.AutorizadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ResponsavelSstUsuario).WithMany()
            .HasForeignKey(p => p.ResponsavelSstUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.SuspensaPorUsuario).WithMany()
            .HasForeignKey(p => p.SuspensaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.RevalidadaPorUsuario).WithMany()
            .HasForeignKey(p => p.RevalidadaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.EncerradaPorUsuario).WithMany()
            .HasForeignKey(p => p.EncerradaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AtividadeId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PermissaoTrabalhoPreRequisitoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoPreRequisito>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoPreRequisito> builder)
    {
        builder.HasOne(r => r.PermissaoTrabalho).WithMany(p => p.PreRequisitos)
            .HasForeignKey(r => r.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.PermissaoTrabalhoId, r.Item }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class PermissaoTrabalhoTipoTrabalhoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoTipoTrabalho>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoTipoTrabalho> builder)
    {
        builder.Property(t => t.DescricaoOutro).HasMaxLength(200);
        builder.HasOne(t => t.PermissaoTrabalho).WithMany(p => p.TiposTrabalho)
            .HasForeignKey(t => t.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.PermissaoTrabalhoId, t.Tipo }).IsUnique();
        builder.HasQueryFilter(t => t.Ativo);
    }
}

public class PermissaoTrabalhoVerificacaoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoVerificacao>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoVerificacao> builder)
    {
        builder.HasOne(v => v.PermissaoTrabalho).WithMany(p => p.Verificacoes)
            .HasForeignKey(v => v.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.PermissaoTrabalhoId, v.Item }).IsUnique();
        builder.HasQueryFilter(v => v.Ativo);
    }
}

public class PermissaoTrabalhoEpiConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoEpi>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoEpi> builder)
    {
        builder.Property(e => e.Complemento).HasMaxLength(100);
        builder.HasOne(e => e.PermissaoTrabalho).WithMany(p => p.Epis)
            .HasForeignKey(e => e.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.PermissaoTrabalhoId, e.Item }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class PermissaoTrabalhoEpcConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoEpc>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoEpc> builder)
    {
        builder.HasOne(e => e.PermissaoTrabalho).WithMany(p => p.Epcs)
            .HasForeignKey(e => e.PermissaoTrabalhoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.PermissaoTrabalhoId, e.Item }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class PermissaoTrabalhoRiscoCriticoConfiguracao : IEntityTypeConfiguration<PermissaoTrabalhoRiscoCritico>
{
    public void Configure(EntityTypeBuilder<PermissaoTrabalhoRiscoCritico> builder)
    {
        builder.Property(r => r.RiscoCondicao).IsRequired().HasMaxLength(300);
        builder.Property(r => r.ControleComplementar).HasMaxLength(500);
        builder.Property(r => r.ResponsavelEvidencia).HasMaxLength(200);
        builder.HasOne(r => r.PermissaoTrabalho).WithMany(p => p.RiscosCriticos)
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
