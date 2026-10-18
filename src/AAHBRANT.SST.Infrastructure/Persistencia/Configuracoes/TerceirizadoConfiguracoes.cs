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

public class AcessoPortalTerceiroConfiguracao : IEntityTypeConfiguration<AcessoPortalTerceiro>
{
    public void Configure(EntityTypeBuilder<AcessoPortalTerceiro> builder)
    {
        builder.Property(a => a.Email).IsRequired().HasMaxLength(200);
        builder.Property(a => a.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.CodigoHash).HasMaxLength(64);
        builder.Property(a => a.SessaoHash).HasMaxLength(64);
        builder.Property(a => a.TermoVersaoAceita).HasMaxLength(20);
        builder.HasIndex(a => a.TokenHash).IsUnique();
        builder.HasIndex(a => a.SessaoHash).HasFilter("[SessaoHash] IS NOT NULL");
        builder.HasIndex(a => a.ContratoId);

        builder.HasOne(a => a.Contrato).WithMany()
            .HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => a.Ativo);
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}

public class DocumentoEnviadoTerceiroConfiguracao : IEntityTypeConfiguration<DocumentoEnviadoTerceiro>
{
    public void Configure(EntityTypeBuilder<DocumentoEnviadoTerceiro> builder)
    {
        builder.Property(d => d.NomeArquivo).IsRequired().HasMaxLength(260);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Conteudo).IsRequired();
        builder.Property(d => d.HashSha256).IsRequired().HasMaxLength(64);
        builder.Property(d => d.InstituicaoInstrutor).HasMaxLength(200);
        builder.Property(d => d.NumeroCertificado).HasMaxLength(100);
        builder.Property(d => d.MotivoRecusa).HasMaxLength(500);
        builder.HasIndex(d => new { d.ContratoId, d.Status });
        builder.HasIndex(d => d.TrabalhadorId);

        builder.HasOne(d => d.AcessoPortalTerceiro).WithMany()
            .HasForeignKey(d => d.AcessoPortalTerceiroId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Contrato).WithMany()
            .HasForeignKey(d => d.ContratoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Trabalhador).WithMany()
            .HasForeignKey(d => d.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.CursoTreinamento).WithMany()
            .HasForeignKey(d => d.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => d.Ativo);
        builder.Property(d => d.RowVersion).IsRowVersion();
    }
}
