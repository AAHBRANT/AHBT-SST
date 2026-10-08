using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class IdeiaConfiguracao : IEntityTypeConfiguration<Ideia>
{
    public void Configure(EntityTypeBuilder<Ideia> builder)
    {
        builder.Property(i => i.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(i => i.MensagemOriginal).HasMaxLength(4000).IsRequired();
        builder.Property(i => i.TelegramUsuarioNome).HasMaxLength(160);
        builder.Property(i => i.RegistradoPorNome).HasMaxLength(160);
        builder.Property(i => i.Titulo).HasMaxLength(180).IsRequired();
        builder.Property(i => i.Descricao).HasMaxLength(4000).IsRequired();
        builder.Property(i => i.ProblemaOportunidade).HasMaxLength(2000);
        builder.Property(i => i.Objetivo).HasMaxLength(2000);
        builder.Property(i => i.SolucaoSugerida).HasMaxLength(2000);
        builder.Property(i => i.Modulo).HasMaxLength(120);
        builder.Property(i => i.Submodulo).HasMaxLength(120);
        builder.Property(i => i.Categoria).HasMaxLength(120);
        builder.Property(i => i.BeneficioEsperado).HasMaxLength(2000);
        builder.Property(i => i.PossiveisImpactos).HasMaxLength(2000);
        builder.Property(i => i.IntegracoesNecessarias).HasMaxLength(1000);
        builder.Property(i => i.Dependencias).HasMaxLength(2000);
        builder.Property(i => i.InformacoesFaltantes).HasMaxLength(2000);
        builder.Property(i => i.EstruturadoPor).HasMaxLength(40).IsRequired();
        builder.Property(i => i.EsforcoEstimado).HasMaxLength(120);
        builder.Property(i => i.ResponsavelAnaliseNome).HasMaxLength(160);
        builder.Property(i => i.Justificativa).HasMaxLength(2000);
        builder.Property(i => i.DecididoPorNome).HasMaxLength(160);
        builder.Property(i => i.ResponsavelDesenvolvimentoNome).HasMaxLength(160);
        builder.Property(i => i.Observacoes).HasMaxLength(4000);

        builder.HasOne(i => i.RegistradoPorUsuario).WithMany()
            .HasForeignKey(i => i.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ResponsavelAnaliseUsuario).WithMany()
            .HasForeignKey(i => i.ResponsavelAnaliseUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ResponsavelDesenvolvimentoUsuario).WithMany()
            .HasForeignKey(i => i.ResponsavelDesenvolvimentoUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.IdeiaPrincipal).WithMany()
            .HasForeignKey(i => i.IdeiaPrincipalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.IdeiaSemelhante).WithMany()
            .HasForeignKey(i => i.IdeiaSemelhanteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.Codigo).IsUnique();
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.Modulo);
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class IdeiaComentarioConfiguracao : IEntityTypeConfiguration<IdeiaComentario>
{
    public void Configure(EntityTypeBuilder<IdeiaComentario> builder)
    {
        builder.Property(c => c.AutorNome).HasMaxLength(160).IsRequired();
        builder.Property(c => c.Texto).HasMaxLength(4000).IsRequired();
        builder.HasOne(c => c.Ideia).WithMany(i => i.Comentarios)
            .HasForeignKey(c => c.IdeiaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.IdeiaId);
        builder.HasQueryFilter(c => c.Ativo);
    }
}

public class IdeiaHistoricoConfiguracao : IEntityTypeConfiguration<IdeiaHistorico>
{
    public void Configure(EntityTypeBuilder<IdeiaHistorico> builder)
    {
        builder.Property(h => h.Descricao).HasMaxLength(1000).IsRequired();
        builder.Property(h => h.AutorNome).HasMaxLength(160).IsRequired();
        builder.HasOne(h => h.Ideia).WithMany(i => i.Historico)
            .HasForeignKey(h => h.IdeiaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(h => h.IdeiaId);
        builder.HasQueryFilter(h => h.Ativo);
    }
}

public class IdeiaAnexoConfiguracao : IEntityTypeConfiguration<IdeiaAnexo>
{
    public void Configure(EntityTypeBuilder<IdeiaAnexo> builder)
    {
        builder.Property(a => a.NomeArquivo).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Conteudo).IsRequired();
        builder.Property(a => a.EnviadoPorNome).HasMaxLength(160);
        builder.HasOne(a => a.Ideia).WithMany(i => i.Anexos)
            .HasForeignKey(a => a.IdeiaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.IdeiaId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class IdeiaRequisitoConfiguracao : IEntityTypeConfiguration<IdeiaRequisito>
{
    public void Configure(EntityTypeBuilder<IdeiaRequisito> builder)
    {
        builder.Property(r => r.Titulo).HasMaxLength(180).IsRequired();
        builder.Property(r => r.Descricao).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.CriteriosAceite).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.AprovadoPorNome).HasMaxLength(160);
        builder.HasOne(r => r.Ideia).WithMany(i => i.Requisitos)
            .HasForeignKey(r => r.IdeiaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.IdeiaId);
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class DemandaDesenvolvimentoConfiguracao : IEntityTypeConfiguration<DemandaDesenvolvimento>
{
    public void Configure(EntityTypeBuilder<DemandaDesenvolvimento> builder)
    {
        builder.Property(d => d.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(d => d.Titulo).HasMaxLength(180).IsRequired();
        builder.Property(d => d.Descricao).HasMaxLength(4000);
        builder.Property(d => d.ResponsavelNome).HasMaxLength(160);
        builder.Property(d => d.FuncionalidadeEntregue).HasMaxLength(500);
        // Restrict nos dois lados: evita múltiplos caminhos de cascade (Ideia → Requisito → Demanda e Ideia → Demanda).
        builder.HasOne(d => d.Requisito).WithMany(r => r.Demandas)
            .HasForeignKey(d => d.RequisitoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Ideia).WithMany()
            .HasForeignKey(d => d.IdeiaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.Codigo).IsUnique();
        builder.HasIndex(d => d.IdeiaId);
        builder.HasQueryFilter(d => d.Ativo);
    }
}
