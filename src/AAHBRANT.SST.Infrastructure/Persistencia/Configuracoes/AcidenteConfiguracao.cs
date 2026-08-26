using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AcidenteConfiguracao : IEntityTypeConfiguration<Acidente>
{
    public void Configure(EntityTypeBuilder<Acidente> builder)
    {
        builder.Property(a => a.Local).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Descricao).IsRequired().HasMaxLength(2000);
        builder.Property(a => a.Lesao).HasMaxLength(500);
        builder.Property(a => a.Consequencia).HasMaxLength(500);
        builder.Property(a => a.Atendimento).HasMaxLength(500);
        builder.Property(a => a.NumeroCat).HasMaxLength(50);
        builder.Property(a => a.Causas).HasMaxLength(2000);

        // A coluna física já é timestamp/rowversion; sem IsRowVersion() o EF tenta inserir valor
        // explícito nela, e o SQL Server rejeita (drift pré-existente, igual ao de
        // OrganizacaoConfiguracoes/AcessoConfiguracoes).
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Trabalhador).WithMany()
            .HasForeignKey(a => a.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Atividade).WithMany()
            .HasForeignKey(a => a.AtividadeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Tipo);
        builder.HasQueryFilter(a => a.Ativo);
    }
}
