using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AsoConfiguracao : IEntityTypeConfiguration<Aso>
{
    public void Configure(EntityTypeBuilder<Aso> builder)
    {
        builder.HasOne(a => a.Trabalhador).WithMany(t => t.Asos)
            .HasForeignKey(a => a.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.TrabalhadorId, a.DataValidade });
        builder.HasQueryFilter(a => a.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}

public class AsoRestricaoConfiguracao : IEntityTypeConfiguration<AsoRestricao>
{
    public void Configure(EntityTypeBuilder<AsoRestricao> builder)
    {
        builder.Property(r => r.Descricao).IsRequired().HasMaxLength(300);
        builder.HasOne(r => r.Aso).WithMany(a => a.Restricoes)
            .HasForeignKey(r => r.AsoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class CursoTreinamentoConfiguracao : IEntityTypeConfiguration<CursoTreinamento>
{
    public void Configure(EntityTypeBuilder<CursoTreinamento> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(150);
        builder.HasQueryFilter(c => c.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class TreinamentoConfiguracao : IEntityTypeConfiguration<Treinamento>
{
    public void Configure(EntityTypeBuilder<Treinamento> builder)
    {
        builder.HasOne(t => t.Trabalhador).WithMany(tr => tr.Treinamentos)
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CursoTreinamento).WithMany(c => c.Realizacoes)
            .HasForeignKey(t => t.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.TrabalhadorId, t.DataValidade });
        builder.HasQueryFilter(t => t.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}

public class CatalogoEpiConfiguracao : IEntityTypeConfiguration<CatalogoEpi>
{
    public void Configure(EntityTypeBuilder<CatalogoEpi> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Fabricante).HasMaxLength(150);
        builder.Property(c => c.CertificadoAprovacaoNumero).HasMaxLength(20);
        builder.HasQueryFilter(c => c.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class EntregaEpiConfiguracao : IEntityTypeConfiguration<EntregaEpi>
{
    public void Configure(EntityTypeBuilder<EntregaEpi> builder)
    {
        builder.Property(e => e.VistoConsorcioResponsavel).HasMaxLength(150);
        builder.Property(e => e.Motivo).HasMaxLength(200);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.EntregasEpi)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CatalogoEpi).WithMany(c => c.Entregas)
            .HasForeignKey(e => e.CatalogoEpiId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TrabalhadorId, e.DataValidade });
        builder.HasQueryFilter(e => e.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class AlertaConfiguracao : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> builder)
    {
        builder.Property(a => a.Titulo).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EntidadeOrigemTipo).IsRequired().HasMaxLength(60);
        builder.HasOne(a => a.Trabalhador).WithMany()
            .HasForeignKey(a => a.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.DestinatarioUsuario).WithMany()
            .HasForeignKey(a => a.DestinatarioUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.EscalonadoParaUsuario).WithMany()
            .HasForeignKey(a => a.EscalonadoParaUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.Status, a.Severidade });
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class AlertaHistoricoEnvioConfiguracao : IEntityTypeConfiguration<AlertaHistoricoEnvio>
{
    public void Configure(EntityTypeBuilder<AlertaHistoricoEnvio> builder)
    {
        builder.Property(h => h.Canal).IsRequired().HasMaxLength(30);
        builder.HasOne(h => h.Alerta).WithMany(a => a.HistoricoEnvios)
            .HasForeignKey(h => h.AlertaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(h => h.Ativo);
    }
}

public class EvidenciaConfiguracao : IEntityTypeConfiguration<Evidencia>
{
    public void Configure(EntityTypeBuilder<Evidencia> builder)
    {
        builder.Property(e => e.EntidadeTipo).IsRequired().HasMaxLength(60);
        builder.Property(e => e.BlobUrl).IsRequired().HasMaxLength(500);
        builder.Property(e => e.NomeArquivo).IsRequired().HasMaxLength(260);
        builder.Property(e => e.HashSha256).IsRequired().HasMaxLength(64);
        builder.HasOne(e => e.AutorUsuario).WithMany()
            .HasForeignKey(e => e.AutorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.EntidadeTipo, e.EntidadeId });
        builder.HasQueryFilter(e => e.Ativo);
    }
}

// Append-only por convenção de aplicação: nenhum repositório deve expor Update/Delete para esta entidade.
public class TrilhaAuditoriaConfiguracao : IEntityTypeConfiguration<TrilhaAuditoria>
{
    public void Configure(EntityTypeBuilder<TrilhaAuditoria> builder)
    {
        builder.Property(t => t.Acao).IsRequired().HasMaxLength(100);
        builder.Property(t => t.EntidadeTipo).IsRequired().HasMaxLength(60);
        builder.Property(t => t.HashRegistroAnterior).IsRequired().HasMaxLength(64);
        builder.Property(t => t.HashRegistroAtual).IsRequired().HasMaxLength(64);
        builder.HasOne(t => t.Usuario).WithMany()
            .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Trabalhador).WithMany()
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.EntidadeTipo, t.EntidadeId, t.Timestamp });
    }
}
