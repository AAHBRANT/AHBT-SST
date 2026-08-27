using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

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

        // Entidade nova, sem coluna varbinary legada — mesmo padrão de MatrizEpiFuncaoConfiguracao.
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}
