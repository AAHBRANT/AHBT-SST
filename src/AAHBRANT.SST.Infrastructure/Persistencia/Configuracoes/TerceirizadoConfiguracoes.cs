using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class EmpresaConfiguracao : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.Property(e => e.RazaoSocial).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NomeFantasia).HasMaxLength(200);
        builder.Property(e => e.Cnpj).IsRequired().HasMaxLength(18);
        builder.Property(e => e.TipoServicoPrestado).HasMaxLength(200);
        builder.Property(e => e.ContatoNome).HasMaxLength(200);
        builder.Property(e => e.ContatoTelefone).HasMaxLength(30);
        builder.Property(e => e.ContatoEmail).HasMaxLength(200);
        builder.HasIndex(e => e.Cnpj).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class ContratoConfiguracao : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.Property(c => c.NumeroContrato).IsRequired().HasMaxLength(50);
        builder.Property(c => c.GJuriContratoId).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.GJuriContratoId).IsUnique();

        builder.HasOne(c => c.Empresa).WithMany(e => e.Contratos)
            .HasForeignKey(c => c.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => c.Ativo);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class ContratoVagaFuncaoConfiguracao : IEntityTypeConfiguration<ContratoVagaFuncao>
{
    public void Configure(EntityTypeBuilder<ContratoVagaFuncao> builder)
    {
        builder.HasOne(v => v.Contrato).WithMany(c => c.Vagas)
            .HasForeignKey(v => v.ContratoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Funcao).WithMany()
            .HasForeignKey(v => v.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.ContratoId, v.FuncaoId }).IsUnique();

        builder.HasQueryFilter(v => v.Ativo);
        builder.Property(v => v.RowVersion).IsRowVersion();
    }
}
