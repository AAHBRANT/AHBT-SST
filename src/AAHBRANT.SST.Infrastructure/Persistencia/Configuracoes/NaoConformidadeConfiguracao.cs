using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class NaoConformidadeConfiguracao : IEntityTypeConfiguration<NaoConformidade>
{
    public void Configure(EntityTypeBuilder<NaoConformidade> builder)
    {
        builder.Property(n => n.Descricao).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.RequisitoRelacionado).HasMaxLength(300);
        builder.Property(n => n.Local).HasMaxLength(200);
        builder.Property(n => n.ObservacoesEncerramento).HasMaxLength(1000);
        builder.Property(n => n.MotivoDevolucao).HasMaxLength(1000);

        builder.HasOne(n => n.Atividade).WithMany()
            .HasForeignKey(n => n.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Risco).WithMany()
            .HasForeignKey(n => n.RiscoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.ResponsavelUsuario).WithMany()
            .HasForeignKey(n => n.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        // Um item de inspeção gera no máximo uma NC (índice único) — idempotência de
        // CriarNaoConformidadeDeItemCommand, mesmo padrão de CriarDocumentoAssinaturaCommand.
        builder.HasOne(n => n.InspecaoItemResposta).WithMany()
            .HasForeignKey(n => n.InspecaoItemRespostaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => n.InspecaoItemRespostaId).IsUnique().HasFilter("[InspecaoItemRespostaId] IS NOT NULL");

        builder.HasIndex(n => n.Status);
        builder.HasQueryFilter(n => n.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(n => n.RowVersion).IsRowVersion();
    }
}
