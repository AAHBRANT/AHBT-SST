using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RelatorioGeradoConfiguracao : IEntityTypeConfiguration<RelatorioGerado>
{
    public void Configure(EntityTypeBuilder<RelatorioGerado> builder)
    {
        builder.Property(r => r.ChaveUnica).IsRequired().HasMaxLength(120);
        builder.Property(r => r.Titulo).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Resumo).IsRequired().HasMaxLength(500);
        builder.Property(r => r.PdfNome).HasMaxLength(200);
        builder.Property(r => r.Imagem).IsRequired();

        // Um relatório por DDS / semana / obra: é o que impede o envio em duplicidade.
        builder.HasIndex(r => r.ChaveUnica).IsUnique();
        builder.HasIndex(r => new { r.Tipo, r.GeradoEm });
        builder.HasIndex(r => new { r.ObraId, r.GeradoEm });

        builder.HasMany(r => r.Envios).WithOne(e => e.RelatorioGerado)
            .HasForeignKey(e => e.RelatorioGeradoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(r => r.Ativo);
    }
}

public class RelatorioEnvioConfiguracao : IEntityTypeConfiguration<RelatorioEnvio>
{
    public void Configure(EntityTypeBuilder<RelatorioEnvio> builder)
    {
        builder.Property(e => e.Erro).HasMaxLength(500);
        builder.HasIndex(e => e.RelatorioGeradoId);
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class DestinatarioRelatorioConfiguracao : IEntityTypeConfiguration<DestinatarioRelatorio>
{
    public void Configure(EntityTypeBuilder<DestinatarioRelatorio> builder)
    {
        builder.HasOne(d => d.Usuario).WithMany().HasForeignKey(d => d.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Obra).WithMany().HasForeignKey(d => d.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.UsuarioId, d.ObraId });
        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class ExecucaoRelatorioAgendadoConfiguracao : IEntityTypeConfiguration<ExecucaoRelatorioAgendado>
{
    public void Configure(EntityTypeBuilder<ExecucaoRelatorioAgendado> builder)
    {
        builder.Property(e => e.Data).HasColumnType("date");
        builder.HasIndex(e => new { e.Tipo, e.Data }).IsUnique();
    }
}
