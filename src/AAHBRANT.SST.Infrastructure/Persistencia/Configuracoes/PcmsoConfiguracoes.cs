using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class PcmsoConfiguracao : IEntityTypeConfiguration<Pcmso>
{
    public void Configure(EntityTypeBuilder<Pcmso> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.MedicoCoordenadorNome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.MedicoCoordenadorCrm).HasMaxLength(50);

        builder.HasOne(p => p.Obra).WithMany()
            .HasForeignKey(p => p.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.MedicoCoordenadorUsuario).WithMany()
            .HasForeignKey(p => p.MedicoCoordenadorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ObraId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class PcmsoItemMatrizConfiguracao : IEntityTypeConfiguration<PcmsoItemMatriz>
{
    public void Configure(EntityTypeBuilder<PcmsoItemMatriz> builder)
    {
        builder.Property(i => i.NomeExame).IsRequired().HasMaxLength(200);

        builder.HasOne(i => i.Pcmso).WithMany(p => p.ItensMatriz)
            .HasForeignKey(i => i.PcmsoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Funcao).WithMany()
            .HasForeignKey(i => i.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Risco).WithMany()
            .HasForeignKey(i => i.RiscoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.PcmsoId, i.FuncaoId });
        builder.HasQueryFilter(i => i.Ativo);
    }
}

public class PcmsoRevisaoConfiguracao : IEntityTypeConfiguration<PcmsoRevisao>
{
    public void Configure(EntityTypeBuilder<PcmsoRevisao> builder)
    {
        builder.Property(r => r.Motivo).IsRequired().HasMaxLength(500);

        builder.HasOne(r => r.Pcmso).WithMany(p => p.Revisoes)
            .HasForeignKey(r => r.PcmsoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ResponsavelUsuario).WithMany()
            .HasForeignKey(r => r.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.PcmsoId, r.NumeroRevisao }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);
    }
}
