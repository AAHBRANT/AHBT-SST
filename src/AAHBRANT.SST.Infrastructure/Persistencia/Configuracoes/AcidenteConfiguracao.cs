using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class AcidenteEnvolvidoConfiguracao : IEntityTypeConfiguration<AcidenteEnvolvido>
{
    public void Configure(EntityTypeBuilder<AcidenteEnvolvido> builder)
    {
        builder.HasOne(e => e.Acidente).WithMany().HasForeignKey(e => e.AcidenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Trabalhador).WithMany().HasForeignKey(e => e.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AcidenteId, e.TrabalhadorId }).IsUnique().HasFilter("[Ativo] = 1");
        builder.HasIndex(e => e.TrabalhadorId);
        builder.Property(e => e.Papel).HasDefaultValue(PapelEnvolvido.Envolvido).HasSentinel((PapelEnvolvido)0);
        builder.HasQueryFilter(e => e.Ativo);
    }
}

public class AcidentePessoaExternaConfiguracao : IEntityTypeConfiguration<AcidentePessoaExterna>
{
    public void Configure(EntityTypeBuilder<AcidentePessoaExterna> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Empresa).HasMaxLength(200);
        // LGPD: criptografados em nível de aplicação (AES-256-GCM), igual ao CPF do Trabalhador, mas sem
        // hash — não há unicidade nem busca por CPF de pessoa externa. O texto cifrado (nonce+tag+base64)
        // é maior que o original, por isso 200 como em Trabalhador.Cpf.
        builder.Property(p => p.Cpf).HasMaxLength(200).HasConversion<CpfCriptografiaConversor>();
        builder.Property(p => p.Telefone).HasMaxLength(200).HasConversion<CpfCriptografiaConversor>();
        builder.Property(p => p.Lesao).HasMaxLength(500);
        builder.Property(p => p.Atendimento).HasMaxLength(500);
        builder.Property(p => p.RowVersion).IsRowVersion();
        builder.HasOne(p => p.Acidente).WithMany().HasForeignKey(p => p.AcidenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.AcidenteId);
        builder.HasQueryFilter(p => p.Ativo);
    }
}

public class AcidenteConfiguracao : IEntityTypeConfiguration<Acidente>
{
    public void Configure(EntityTypeBuilder<Acidente> builder)
    {
        builder.Property(a => a.Local).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Descricao).IsRequired().HasMaxLength(2000);
        builder.Property(a => a.Lesao).HasMaxLength(500);
        builder.Property(a => a.RegioesCorpo).HasMaxLength(2000);
        builder.Property(a => a.Consequencia).HasMaxLength(500);
        builder.Property(a => a.Atendimento).HasMaxLength(500);
        builder.Property(a => a.NumeroCat).HasMaxLength(50);
        builder.Property(a => a.Causas).HasMaxLength(2000);
        builder.Property(a => a.ConclusaoInvestigacao).HasMaxLength(4000);
        builder.Property(a => a.LaudoObservacao).HasMaxLength(500);
        builder.Property(a => a.ProvidenciasObitoJson).HasMaxLength(4000);

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
