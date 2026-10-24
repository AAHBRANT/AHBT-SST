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
        builder.Property(e => e.Cnpj).IsRequired().HasMaxLength(14);
        builder.Property(e => e.TipoServicoPrestado).HasMaxLength(200);
        builder.Property(e => e.ContatoNome).HasMaxLength(200);
        builder.Property(e => e.ContatoTelefone).HasMaxLength(30);
        builder.Property(e => e.ContatoEmail).HasMaxLength(200);
        builder.HasIndex(e => e.Cnpj).IsUnique();
        builder.HasQueryFilter(e => e.Ativo);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class ArquivoContratoConfiguracao : IEntityTypeConfiguration<ArquivoContrato>
{
    public void Configure(EntityTypeBuilder<ArquivoContrato> builder)
    {
        builder.ToTable("ArquivosContrato");
        builder.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(260);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(150);
        // Um arquivo por contrato; enviar de novo substitui. Cascade: o arquivo não tem vida própria.
        builder.HasOne(a => a.Contrato).WithOne(c => c.Arquivo)
            .HasForeignKey<ArquivoContrato>(a => a.ContratoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.ContratoId).IsUnique();
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class DocumentoExigidoEmpresaConfiguracao : IEntityTypeConfiguration<DocumentoExigidoEmpresa>
{
    public void Configure(EntityTypeBuilder<DocumentoExigidoEmpresa> builder)
    {
        builder.ToTable("DocumentosExigidosEmpresa");
        builder.Property(d => d.Nome).IsRequired().HasMaxLength(120);
        builder.Property(d => d.Descricao).HasMaxLength(500);
        // Sem filtro de Ativo: desativado continua aparecendo na configuração (para reativar) e
        // nos documentos já enviados; quem monta o checklist filtra por Ativo.
    }
}

public class DocumentoEmpresaTerceirizadaConfiguracao : IEntityTypeConfiguration<DocumentoEmpresaTerceirizada>
{
    public void Configure(EntityTypeBuilder<DocumentoEmpresaTerceirizada> builder)
    {
        builder.ToTable("DocumentosEmpresaTerceirizada");
        builder.Property(d => d.NomeArquivo).IsRequired().HasMaxLength(260);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(150);
        builder.HasOne(d => d.Empresa).WithMany(e => e.Documentos)
            .HasForeignKey(d => d.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.DocumentoExigido).WithMany()
            .HasForeignKey(d => d.DocumentoExigidoEmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.EmpresaId, d.DocumentoExigidoEmpresaId }).IsUnique();
        builder.HasQueryFilter(d => d.Ativo);
    }
}

public class ArquivoDocumentoEmpresaTerceirizadaConfiguracao : IEntityTypeConfiguration<ArquivoDocumentoEmpresaTerceirizada>
{
    public void Configure(EntityTypeBuilder<ArquivoDocumentoEmpresaTerceirizada> builder)
    {
        builder.ToTable("ArquivosDocumentoEmpresaTerceirizada");
        builder.HasOne(a => a.Documento).WithOne(d => d.Arquivo)
            .HasForeignKey<ArquivoDocumentoEmpresaTerceirizada>(a => a.DocumentoEmpresaTerceirizadaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.DocumentoEmpresaTerceirizadaId).IsUnique();
        builder.HasQueryFilter(a => a.Ativo);
    }
}

public class ContratoConfiguracao : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.Property(c => c.NumeroContrato).IsRequired().HasMaxLength(50);
        builder.Property(c => c.GJuriContratoId).HasMaxLength(100);
        builder.HasIndex(c => c.GJuriContratoId).IsUnique().HasFilter("[GJuriContratoId] IS NOT NULL");
        builder.Ignore(c => c.VeioDoGJuri);

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
