using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ExameComplementarConfiguracao : IEntityTypeConfiguration<ExameComplementar>
{
    public void Configure(EntityTypeBuilder<ExameComplementar> builder)
    {
        builder.Property(e => e.Resultado).IsRequired().HasMaxLength(300);
        builder.Property(e => e.ResponsavelTecnico).HasMaxLength(150);
        builder.HasOne(e => e.Trabalhador).WithMany(t => t.ExamesComplementares)
            .HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Aso).WithMany()
            .HasForeignKey(e => e.AsoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TrabalhadorId, e.DataValidade });
        builder.HasQueryFilter(e => e.Ativo);

        // Entidade nova — sem coluna varbinary legada, então IsRowVersion() já gera a coluna
        // "rowversion" corretamente na primeira migration (ver AsoConfiguracao para o bug retroativo
        // que isso evita).
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class AptidaoAtividadeEspecificaConfiguracao : IEntityTypeConfiguration<AptidaoAtividadeEspecifica>
{
    public void Configure(EntityTypeBuilder<AptidaoAtividadeEspecifica> builder)
    {
        builder.Property(a => a.AtividadeCritica).IsRequired().HasMaxLength(150);
        builder.Property(a => a.MedicoResponsavel).HasMaxLength(150);
        builder.HasOne(a => a.Trabalhador).WithMany(t => t.AptidoesAtividadeEspecifica)
            .HasForeignKey(a => a.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.TrabalhadorId, a.DataValidade });
        builder.HasQueryFilter(a => a.Ativo);

        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}

public class PcmsoDetalheConfiguracao : IEntityTypeConfiguration<PcmsoDetalhe>
{
    public void Configure(EntityTypeBuilder<PcmsoDetalhe> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Versao).HasMaxLength(50);
        builder.Property(p => p.Arquivo).HasMaxLength(500);
        builder.Property(p => p.MedicoResponsavelNome).HasMaxLength(150);
        builder.Property(p => p.MedicoResponsavelCrm).HasMaxLength(30);

        builder.HasOne(p => p.ResponsavelUsuario).WithMany()
            .HasForeignKey(p => p.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Obra).WithMany()
            .HasForeignKey(p => p.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Setor).WithMany()
            .HasForeignKey(p => p.SetorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.Ativo);

        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
