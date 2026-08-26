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
        builder.HasOne(s => s.DocumentoAssinatura).WithMany(d => d.Signatarios)
            .HasForeignKey(s => s.DocumentoAssinaturaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Trabalhador).WithMany()
            .HasForeignKey(s => s.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);

        // Idempotência (mesmo padrão de RegistrarParticipanteCommand): um trabalhador não pode
        // assinar o mesmo documento duas vezes.
        builder.HasIndex(s => new { s.DocumentoAssinaturaId, s.TrabalhadorId }).IsUnique();

        builder.HasQueryFilter(s => s.Ativo);
    }
}

public class CredencialWebAuthnConfiguracao : IEntityTypeConfiguration<CredencialWebAuthn>
{
    public void Configure(EntityTypeBuilder<CredencialWebAuthn> builder)
    {
        builder.HasOne(c => c.Trabalhador).WithMany()
            .HasForeignKey(c => c.TrabalhadorId).OnDelete(DeleteBehavior.Cascade);

        // CredentialId é o identificador que o leitor/celular devolve a cada assinatura — precisa ser
        // único globalmente (o mesmo autenticador nunca gera dois IDs iguais para credenciais
        // diferentes) para localizar a credencial certa antes de verificar a assinatura.
        builder.HasIndex(c => c.CredentialId).IsUnique();
        builder.HasIndex(c => c.TrabalhadorId);

        builder.HasQueryFilter(c => c.Ativo);
    }
}
