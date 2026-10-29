using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CronogramaMensalDdsConfiguracao : IEntityTypeConfiguration<CronogramaMensalDds>
{
    public void Configure(EntityTypeBuilder<CronogramaMensalDds> b)
    {
        b.Property(x => x.NumeroDocumento).HasMaxLength(50);
        b.Property(x => x.ProgramacaoJson).IsRequired();
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasIndex(x => new { x.ObraId, x.Ano, x.Mes }).IsUnique().HasFilter("[Ativo] = 1");
        b.HasOne(x => x.Obra).WithMany().HasForeignKey(x => x.ObraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResponsavelUsuario).WithMany().HasForeignKey(x => x.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CronogramaMensalDdsRevisaoConfiguracao : IEntityTypeConfiguration<CronogramaMensalDdsRevisao>
{
    public void Configure(EntityTypeBuilder<CronogramaMensalDdsRevisao> b)
    {
        b.Property(x => x.HashPlanejamento).HasMaxLength(64);
        b.Property(x => x.ConteudoJson).IsRequired();
        b.HasIndex(x => new { x.CronogramaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.CronogramaId, x.HashPlanejamento }).IsUnique();
        b.HasOne(x => x.Cronograma).WithMany().HasForeignKey(x => x.CronogramaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DocumentoAssinatura).WithMany().HasForeignKey(x => x.DocumentoAssinaturaId).OnDelete(DeleteBehavior.Restrict);
    }
}
