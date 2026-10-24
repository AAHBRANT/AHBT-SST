using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

// Fila de saída da integração com o G-QUALI. Sem FK para a NC: o evento de cancelamento sobrevive à exclusão.
public class EventoNcGQualiConfiguracao : IEntityTypeConfiguration<EventoNcGQuali>
{
    public void Configure(EntityTypeBuilder<EventoNcGQuali> builder)
    {
        builder.ToTable("EventosNcGQuali");
        builder.Property(e => e.EventId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Tipo).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.Situacao).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RespostaGQuali).HasMaxLength(20);
        builder.Property(e => e.UltimoErro).HasMaxLength(1000);
        builder.HasIndex(e => e.EventId).IsUnique();
        builder.HasIndex(e => new { e.Situacao, e.ProximaTentativaEmUtc });
        builder.HasIndex(e => e.NaoConformidadeId);
    }
}
