using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class ObraConfiguracao : IEntityTypeConfiguration<Obra>
{
    public void Configure(EntityTypeBuilder<Obra> builder)
    {
        builder.Property(o => o.Codigo).IsRequired().HasMaxLength(30);
        builder.Property(o => o.Nome).IsRequired().HasMaxLength(200);
        builder.HasIndex(o => o.Codigo).IsUnique();
        builder.HasQueryFilter(o => o.Ativo);
        builder.Property(o => o.MetodosAutenticacaoHabilitados).IsRequired();

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(o => o.RowVersion).IsRowVersion();
    }
}

public class SetorConfiguracao : IEntityTypeConfiguration<Setor>
{
    public void Configure(EntityTypeBuilder<Setor> builder)
    {
        builder.Property(s => s.Nome).IsRequired().HasMaxLength(150);
        builder.HasOne(s => s.Obra).WithMany(o => o.Setores)
            .HasForeignKey(s => s.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(s => s.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}

public class EquipeConfiguracao : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.Property(e => e.Nome).IsRequired().HasMaxLength(150);
        builder.HasOne(e => e.Setor).WithMany(s => s.Equipes)
            .HasForeignKey(e => e.SetorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Encarregado).WithMany()
            .HasForeignKey(e => e.EncarregadoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(e => e.Ativo);

        // Mesmo bug já corrigido para Acidentes (ver migration CorrigirRowVersionAcidentes): sem
        // IsRowVersion() o EF tenta INSERT com valor explícito na coluna RowVersion, e o SQL Server
        // rejeita. A migration seguinte recria a coluna física como "rowversion" de fato.
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class FuncaoConfiguracao : IEntityTypeConfiguration<Funcao>
{
    public void Configure(EntityTypeBuilder<Funcao> builder)
    {
        builder.Property(f => f.Nome).IsRequired().HasMaxLength(150);
        builder.Property(f => f.CboCodigo).HasMaxLength(10);
        builder.HasQueryFilter(f => f.Ativo);
        // Mesma divergência pré-existente de schema descrita em PermissaoConfiguracao (AcessoConfiguracoes.cs):
        // a coluna física já é timestamp/rowversion; sem IsRowVersion() o EF tenta inserir valor explícito
        // nela e o SQL Server rejeita o INSERT. Confirmado via sys.columns antes de aplicar.
        builder.Property(f => f.RowVersion).IsRowVersion();
    }
}

public class TrabalhadorConfiguracao : IEntityTypeConfiguration<Trabalhador>
{
    public void Configure(EntityTypeBuilder<Trabalhador> builder)
    {
        builder.Property(t => t.Nome).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Matricula).IsRequired().HasMaxLength(30);
        // Cpf: criptografado em repouso via AES-256-GCM (LGPD art. 46) — o valor de coluna nunca é o
        // CPF em texto puro. HasMaxLength(200) acomoda nonce+tag+ciphertext em Base64 (bem maior que
        // os 11 dígitos originais). Unicidade não pode mais viver em Cpf (ciphertext não-determinístico
        // por nonce aleatório) — migrou para CpfHash, com índice filtrado para tolerar linhas legadas
        // ainda não migradas pelo seeder de backfill até ele rodar.
        builder.Property(t => t.Cpf).IsRequired().HasMaxLength(200).HasConversion<CpfCriptografiaConversor>();
        builder.Property(t => t.CpfHash).HasMaxLength(64);
        builder.HasIndex(t => t.CpfHash).IsUnique().HasFilter("[CpfHash] IS NOT NULL");
        builder.HasIndex(t => new { t.ObraId, t.Matricula }).IsUnique();

        // Motor de Assinatura Eletrônica — PinHash é auto-contido (algoritmo+iterações+salt+hash em
        // uma string, ver PinHasher), por isso o tamanho generoso; não é indexado (nunca é buscado
        // por valor, só verificado contra o Id do trabalhador já conhecido).
        builder.Property(t => t.PinHash).HasMaxLength(200);

        builder.HasOne(t => t.Obra).WithMany(o => o.Trabalhadores)
            .HasForeignKey(t => t.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Setor).WithMany()
            .HasForeignKey(t => t.SetorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Equipe).WithMany(e => e.Trabalhadores)
            .HasForeignKey(t => t.EquipeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Funcao).WithMany(f => f.Trabalhadores)
            .HasForeignKey(t => t.FuncaoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => t.Ativo);

        // Mesma divergência pré-existente de schema descrita em PermissaoConfiguracao (AcessoConfiguracoes.cs):
        // a coluna física já é timestamp/rowversion; sem IsRowVersion() o EF tenta inserir valor explícito
        // nela e o SQL Server rejeita o INSERT. Confirmado via sys.columns antes de aplicar.
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
