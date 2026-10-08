using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class DocumentoAssinaturaConfiguracao : IEntityTypeConfiguration<DocumentoAssinatura>
{
    public void Configure(EntityTypeBuilder<DocumentoAssinatura> builder)
    {
        builder.Property(d => d.EntidadeTipo).IsRequired().HasMaxLength(50);
        builder.Property(d => d.ConteudoHash).HasMaxLength(64);
        builder.Property(d => d.HashPdf).HasMaxLength(64);
        builder.Property(d => d.TokenValidacaoPublica).HasMaxLength(64);

        builder.HasIndex(d => new { d.EntidadeTipo, d.EntidadeId });
        // Filtrado: só documentos finalizados têm token — evita colisão de múltiplos NULL sob índice
        // único (SQL Server trata cada NULL como distinto, mas o filtro deixa a intenção explícita).
        builder.HasIndex(d => d.TokenValidacaoPublica).IsUnique().HasFilter("[TokenValidacaoPublica] IS NOT NULL");

        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class DocumentoSignatarioConfiguracao : IEntityTypeConfiguration<DocumentoSignatario>
{
    public void Configure(EntityTypeBuilder<DocumentoSignatario> builder)
    {
        builder.Property(s => s.FotoEvidenciaContentType).HasMaxLength(80);
        builder.Property(s => s.FotoEvidenciaHash).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(300);
        builder.Property(s => s.ValidacaoModelo).HasMaxLength(60);
        builder.Property(s => s.ValidacaoGrupoId).HasMaxLength(80);
        builder.Property(s => s.ValidacaoRequisicaoId).HasMaxLength(80);

        builder.HasOne(s => s.DocumentoAssinatura).WithMany(d => d.Signatarios)
            .HasForeignKey(s => s.DocumentoAssinaturaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Trabalhador).WithMany()
            .HasForeignKey(s => s.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        // Idempotência por papel: em EntregaEpi um Técnico de Segurança pode assinar como
        // recebedor e como responsável, desde que use métodos distintos. A regra de aplicação
        // bloqueia a repetição do mesmo método; o índice precisa refletir esse contrato. O Papel
        // entra no índice para o técnico assinar o Registro Semanal de DDS nas duas funções (ambas
        // por sessão logada); sem papel (nulo) o comportamento é o de sempre.
        builder.HasIndex(s => new { s.DocumentoAssinaturaId, s.TrabalhadorId, s.MetodoAutenticacao, s.Papel })
            .IsUnique()
            // Sem filtro: o EF poria "[Papel] IS NOT NULL" por a coluna ser anulável, e as assinaturas
            // comuns (Papel nulo) perderiam a garantia de unicidade. No SQL Server o índice único
            // trata nulos como iguais, que é o que queremos.
            .HasFilter(null);

        builder.HasQueryFilter(s => s.Ativo);
    }
}

public class DispositivoAgenteBiometricoConfiguracao : IEntityTypeConfiguration<DispositivoAgenteBiometrico>
{
    public void Configure(EntityTypeBuilder<DispositivoAgenteBiometrico> builder)
    {
        builder.HasOne(d => d.Obra).WithMany()
            .HasForeignKey(d => d.ObraId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.Nome).IsRequired().HasMaxLength(120);
        builder.Property(d => d.SegredoHash).IsRequired().HasMaxLength(128);

        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class TemplateBiometricoFutronicConfiguracao : IEntityTypeConfiguration<TemplateBiometricoFutronic>
{
    public void Configure(EntityTypeBuilder<TemplateBiometricoFutronic> builder)
    {
        // TemplateCriptografado é opaco para o EF — nunca HasConversion<T> aqui. Ver rationale
        // completo em TemplateBiometricoCriptografiaConversor.cs.
        builder.Property(t => t.TemplateCriptografado).IsRequired().HasMaxLength(4000);

        builder.HasOne(t => t.Trabalhador).WithMany()
            .HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => t.Ativo);
    }
}

public class FotoCadastroFacialConfiguracao : IEntityTypeConfiguration<FotoCadastroFacial>
{
    public void Configure(EntityTypeBuilder<FotoCadastroFacial> builder)
    {
        builder.Property(f => f.Conteudo).IsRequired();
        builder.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(f => f.HashSha256).IsRequired().HasMaxLength(64);

        builder.HasOne(f => f.Trabalhador).WithMany()
            .HasForeignKey(f => f.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.TrabalhadorId);
        builder.HasQueryFilter(f => f.Ativo);
    }
}

public class FalhaReconhecimentoFacialConfiguracao : IEntityTypeConfiguration<FalhaReconhecimentoFacial>
{
    public void Configure(EntityTypeBuilder<FalhaReconhecimentoFacial> builder)
    {
        builder.HasOne(f => f.Trabalhador).WithMany()
            .HasForeignKey(f => f.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.ObraId, f.OcorridaEm });
        builder.HasIndex(f => new { f.TrabalhadorId, f.OcorridaEm });
        builder.HasQueryFilter(f => f.Ativo);
    }
}
