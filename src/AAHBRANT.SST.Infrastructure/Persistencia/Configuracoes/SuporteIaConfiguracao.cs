using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class SuporteIaConfiguracao : IEntityTypeConfiguration<SuporteIaSolicitacao>
{
    public void Configure(EntityTypeBuilder<SuporteIaSolicitacao> builder)
    {
        builder.Property(s => s.Titulo).HasMaxLength(180).IsRequired();
        builder.Property(s => s.Descricao).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.Modulo).HasMaxLength(120);
        builder.Property(s => s.UrlContexto).HasMaxLength(800);
        builder.Property(s => s.SolicitanteNome).HasMaxLength(160);
        builder.Property(s => s.SolicitanteEmail).HasMaxLength(256);
        builder.Property(s => s.RespostaAoUsuario).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.DemandaReduzida).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.SolucaoProposta).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.EvidenciasTecnicas).HasMaxLength(4000);
        builder.Property(s => s.ResponsavelNome).HasMaxLength(160);
        builder.Property(s => s.NotaFechamento).HasMaxLength(4000);
        builder.Property(s => s.ComentarioValidacao).HasMaxLength(4000);

        builder.HasOne(s => s.SolicitanteUsuario).WithMany()
            .HasForeignKey(s => s.SolicitanteUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ResponsavelUsuario).WithMany()
            .HasForeignKey(s => s.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Alerta).WithMany()
            .HasForeignKey(s => s.AlertaId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.ResultadoTriagem);
        builder.HasQueryFilter(s => s.Ativo);
    }
}
