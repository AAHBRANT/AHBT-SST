using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class TermoCompromissoEpiManualConfiguracao : IEntityTypeConfiguration<TermoCompromissoEpiManual>
{
    public void Configure(EntityTypeBuilder<TermoCompromissoEpiManual> builder)
    {
        builder.ToTable("TermosCompromissoEpiManual");
        builder.Property(t => t.Observacao).HasMaxLength(500);
        builder.Property(t => t.ArquivoNome).HasMaxLength(260);
        builder.Property(t => t.ArquivoContentType).HasMaxLength(150);
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.Trabalhador).WithMany().HasForeignKey(t => t.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.RegistradoPorUsuario).WithMany().HasForeignKey(t => t.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Um registro ATIVO por trabalhador. O filtro permite registrar de novo depois que o
        // Administrador remove (a remoção é lógica: Ativo = false).
        builder.HasIndex(t => t.TrabalhadorId).IsUnique().HasFilter("[Ativo] = 1");
        builder.HasQueryFilter(t => t.Ativo);
    }
}
