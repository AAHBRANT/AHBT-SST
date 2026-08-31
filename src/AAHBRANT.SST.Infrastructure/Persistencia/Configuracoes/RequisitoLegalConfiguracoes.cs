using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class RequisitoLegalConfiguracao : IEntityTypeConfiguration<RequisitoLegal>
{
    public void Configure(EntityTypeBuilder<RequisitoLegal> builder)
    {
        builder.Property(r => r.Norma).IsRequired().HasMaxLength(60);
        builder.Property(r => r.Artigo).HasMaxLength(60);
        builder.Property(r => r.Titulo).IsRequired().HasMaxLength(300);
        builder.Property(r => r.Descricao).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.Fonte).HasMaxLength(500);
        builder.HasIndex(r => r.Categoria);
        builder.HasQueryFilter(r => r.Ativo);

        // Entidade nova, sem coluna varbinary legada — IsRowVersion() já gera a coluna "rowversion"
        // corretamente na primeira migration (mesmo padrão de MatrizEpiFuncaoConfiguracao).
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}

public class RequisitoLegalCriterioConfiguracao : IEntityTypeConfiguration<RequisitoLegalCriterio>
{
    public void Configure(EntityTypeBuilder<RequisitoLegalCriterio> builder)
    {
        builder.HasOne(c => c.RequisitoLegal).WithMany(r => r.Criterios)
            .HasForeignKey(c => c.RequisitoLegalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Perigo).WithMany()
            .HasForeignKey(c => c.PerigoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Funcao).WithMany()
            .HasForeignKey(c => c.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ItemQuestionarioAplicabilidade).WithMany()
            .HasForeignKey(c => c.ItemQuestionarioAplicabilidadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(c => c.Ativo);

        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class ItemQuestionarioAplicabilidadeConfiguracao : IEntityTypeConfiguration<ItemQuestionarioAplicabilidade>
{
    public void Configure(EntityTypeBuilder<ItemQuestionarioAplicabilidade> builder)
    {
        builder.Property(i => i.Pergunta).IsRequired().HasMaxLength(500);
        builder.Property(i => i.TextoApoio).HasMaxLength(500);
        builder.HasQueryFilter(i => i.Ativo);

        builder.Property(i => i.RowVersion).IsRowVersion();
    }
}

public class RespostaQuestionarioAplicabilidadeConfiguracao : IEntityTypeConfiguration<RespostaQuestionarioAplicabilidade>
{
    public void Configure(EntityTypeBuilder<RespostaQuestionarioAplicabilidade> builder)
    {
        builder.Property(r => r.Observacao).HasMaxLength(500);
        builder.HasOne(r => r.Obra).WithMany()
            .HasForeignKey(r => r.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Item).WithMany()
            .HasForeignKey(r => r.ItemQuestionarioAplicabilidadeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.ObraId, r.ItemQuestionarioAplicabilidadeId }).IsUnique();
        builder.HasQueryFilter(r => r.Ativo);

        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}

public class MatrizTreinamentoFuncaoConfiguracao : IEntityTypeConfiguration<MatrizTreinamentoFuncao>
{
    public void Configure(EntityTypeBuilder<MatrizTreinamentoFuncao> builder)
    {
        builder.HasOne(m => m.Funcao).WithMany()
            .HasForeignKey(m => m.FuncaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.CursoTreinamento).WithMany()
            .HasForeignKey(m => m.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.FuncaoId, m.CursoTreinamentoId }).IsUnique();
        builder.HasQueryFilter(m => m.Ativo);

        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}
