using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AtividadeConfiguracao : IEntityTypeConfiguration<Atividade>
{
    public void Configure(EntityTypeBuilder<Atividade> builder)
    {
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(200);
        builder.Property(a => a.CondicoesExecucao).HasMaxLength(2000);
        builder.Property(a => a.Ferramentas).HasMaxLength(2000);
        builder.HasOne(a => a.Obra).WithMany(o => o.Atividades)
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Ghe).WithMany(g => g.Atividades)
            .HasForeignKey(a => a.GheId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.ObraId);
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class AtividadeEpcConfiguracao : IEntityTypeConfiguration<AtividadeEpc>
{
    public void Configure(EntityTypeBuilder<AtividadeEpc> builder)
    {
        builder.ToTable("AtividadeEpcs");
        builder.HasOne(e => e.Atividade).WithMany(a => a.Epcs).HasForeignKey(e => e.AtividadeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.CatalogoEpc).WithMany().HasForeignKey(e => e.CatalogoEpcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AtividadeId, e.CatalogoEpcId }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class AtividadeDocumentoConfiguracao : IEntityTypeConfiguration<AtividadeDocumento>
{
    public void Configure(EntityTypeBuilder<AtividadeDocumento> builder)
    {
        builder.ToTable("AtividadeDocumentos");
        builder.HasOne(e => e.Atividade).WithMany(a => a.Documentos).HasForeignKey(e => e.AtividadeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MaterialApoio).WithMany().HasForeignKey(e => e.MaterialApoioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AtividadeId, e.MaterialApoioId }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class AtividadeItemOsConfiguracao : IEntityTypeConfiguration<AtividadeItemOs>
{
    public void Configure(EntityTypeBuilder<AtividadeItemOs> builder)
    {
        builder.ToTable("AtividadeItensOs");
        builder.Property(i => i.Texto).IsRequired().HasMaxLength(500);
        builder.Property(i => i.Requisito).HasMaxLength(300);
        builder.Property(i => i.ValidadoPorNome).HasMaxLength(200);
        builder.HasOne(i => i.Atividade).WithMany(a => a.ItensOs).HasForeignKey(i => i.AtividadeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => new { i.AtividadeId, i.Tipo });
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class PerigoConfiguracao : IEntityTypeConfiguration<Perigo>
{
    public void Configure(EntityTypeBuilder<Perigo> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class RiscoConfiguracao : IEntityTypeConfiguration<Risco>
{
    public void Configure(EntityTypeBuilder<Risco> builder)
    {
        builder.HasOne(r => r.Atividade).WithMany(a => a.Riscos)
            .HasForeignKey(r => r.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Perigo).WithMany(p => p.Riscos)
            .HasForeignKey(r => r.PerigoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.AvaliadoPorNome).HasMaxLength(200);
        builder.HasIndex(r => new { r.AtividadeId, r.NivelRisco });
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class MedidaPrevencaoConfiguracao : IEntityTypeConfiguration<MedidaPrevencao>
{
    public void Configure(EntityTypeBuilder<MedidaPrevencao> builder)
    {
        builder.ToTable("MedidasPrevencao");
        builder.Property(m => m.Texto).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.ValidadoPorNome).HasMaxLength(200);
        builder.HasOne(m => m.Risco).WithMany(r => r.Medidas)
            .HasForeignKey(m => m.RiscoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => new { m.RiscoId, m.Fase, m.Ordem });
        builder.HasQueryFilter(m => m.Ativo);
    }
}

public class RiscoTrabalhadorExpostoConfiguracao : IEntityTypeConfiguration<RiscoTrabalhadorExposto>
{
    public void Configure(EntityTypeBuilder<RiscoTrabalhadorExposto> builder)
    {
        builder.HasOne(e => e.Risco).WithMany(r => r.TrabalhadoresExpostos)
            .HasForeignKey(e => e.RiscoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.RiscosExpostos)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.RiscoId, e.TrabalhadorId }).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class MatrizRiscoConfigConfiguracao : IEntityTypeConfiguration<MatrizRiscoConfig>
{
    public void Configure(EntityTypeBuilder<MatrizRiscoConfig> builder)
    {
        builder.Property(m => m.Nome).IsRequired().HasMaxLength(150);
        builder.HasQueryFilter(m => m.Ativo);
    }
}

public class MatrizRiscoCelulaConfiguracao : IEntityTypeConfiguration<MatrizRiscoCelula>
{
    public void Configure(EntityTypeBuilder<MatrizRiscoCelula> builder)
    {
        builder.HasOne(c => c.MatrizRiscoConfig).WithMany(m => m.Celulas)
            .HasForeignKey(c => c.MatrizRiscoConfigId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.MatrizRiscoConfigId, c.Probabilidade, c.Severidade }).IsUnique();
        builder.HasQueryFilter(c => c.Ativo);
    }
}

public class GheConfiguracao : IEntityTypeConfiguration<Ghe>
{
    public void Configure(EntityTypeBuilder<Ghe> builder)
    {
        builder.Property(g => g.Setor).HasMaxLength(200);
        builder.Property(g => g.JornadaTrabalho).HasMaxLength(200);
        builder.Property(g => g.AtividadesCriticas).HasMaxLength(500);
        builder.HasOne(g => g.Obra).WithMany()
            .HasForeignKey(g => g.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.ObraId, g.Numero });
        builder.HasQueryFilter(g => g.Ativo);
    }
}

public class GheFuncaoConfiguracao : IEntityTypeConfiguration<GheFuncao>
{
    public void Configure(EntityTypeBuilder<GheFuncao> builder)
    {
        builder.HasOne(f => f.Ghe).WithMany(g => g.Funcoes)
            .HasForeignKey(f => f.GheId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(f => f.Funcao).WithMany()
            .HasForeignKey(f => f.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.GheId, f.FuncaoId });
        builder.HasQueryFilter(f => f.Ativo);
    }
}
