using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

// "Realizar integração". Sem navegação para o pai nas entidades do atendimento (id + foto do momento): o filtro de
// escopo por obra do SstDbContext substitui o de Ativo e cada entidade tem ObraId próprio. As chaves estrangeiras
// existem só para integridade (sem navegação, nada vira INNER JOIN com registro excluído).
public class IntegracaoAtendimentoConfiguracao : IEntityTypeConfiguration<IntegracaoAtendimento>
{
    public void Configure(EntityTypeBuilder<IntegracaoAtendimento> b)
    {
        b.ToTable("IntegracoesAtendimento");
        b.Property(x => x.Numero).HasMaxLength(32).IsRequired();
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasIndex(x => x.Numero).IsUnique();
        // Um atendimento em andamento por obra (1 = EmAndamento).
        b.HasIndex(x => x.ObraId).IsUnique().HasFilter("[Ativo] = 1 AND [Status] = 1")
            .HasDatabaseName("IX_IntegracoesAtendimento_ObraId_EmAndamento");
        b.HasOne<Obra>().WithMany().HasForeignKey(x => x.ObraId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Trabalhador>().WithMany().HasForeignKey(x => x.InstrutorTrabalhadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntegracaoEtapaConfiguracao : IEntityTypeConfiguration<IntegracaoEtapa>
{
    public void Configure(EntityTypeBuilder<IntegracaoEtapa> b)
    {
        b.ToTable("IntegracaoEtapas");
        b.HasIndex(x => new { x.IntegracaoAtendimentoId, x.Ordem }).IsUnique();
        b.HasIndex(x => x.SessaoTreinamentoId).HasFilter("[SessaoTreinamentoId] IS NOT NULL");
        b.HasOne<IntegracaoAtendimento>().WithMany().HasForeignKey(x => x.IntegracaoAtendimentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CursoTreinamento>().WithMany().HasForeignKey(x => x.CursoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SessaoTreinamento>().WithMany().HasForeignKey(x => x.SessaoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ParticipanteIntegracaoConfiguracao : IEntityTypeConfiguration<ParticipanteIntegracao>
{
    public void Configure(EntityTypeBuilder<ParticipanteIntegracao> b)
    {
        b.ToTable("ParticipantesIntegracao");
        b.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        b.Property(x => x.FuncaoNome).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.IntegracaoAtendimentoId, x.TrabalhadorId }).IsUnique().HasFilter("[Ativo] = 1");
        b.HasIndex(x => x.TrabalhadorId);
        b.HasOne<IntegracaoAtendimento>().WithMany().HasForeignKey(x => x.IntegracaoAtendimentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlanoIntegracao>().WithMany().HasForeignKey(x => x.PlanoIntegracaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Trabalhador>().WithMany().HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PeriodoEtapaIntegracaoConfiguracao : IEntityTypeConfiguration<PeriodoEtapaIntegracao>
{
    public void Configure(EntityTypeBuilder<PeriodoEtapaIntegracao> b)
    {
        b.ToTable("PeriodosEtapaIntegracao");
        b.Property(x => x.Data).HasColumnType("date");
        b.HasIndex(x => x.IntegracaoEtapaId);
        b.HasOne<IntegracaoEtapa>().WithMany().HasForeignKey(x => x.IntegracaoEtapaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RegistroIntegracaoConfiguracao : IEntityTypeConfiguration<RegistroIntegracao>
{
    public void Configure(EntityTypeBuilder<RegistroIntegracao> b)
    {
        b.ToTable("RegistrosIntegracao");
        b.Property(x => x.TecnicoNome).HasMaxLength(200).IsRequired();
        b.Property(x => x.FuncaoNome).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.IntegracaoAtendimentoId, x.TrabalhadorId }).IsUnique().HasFilter("[Ativo] = 1");
        b.HasIndex(x => x.TrabalhadorId);
        b.HasOne<IntegracaoAtendimento>().WithMany().HasForeignKey(x => x.IntegracaoAtendimentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Trabalhador>().WithMany().HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

// Vínculo da turma com o atendimento e hash da foto de evidência (a mesma foto nunca vale para dois cursos).
public class SessaoVinculoIntegracaoConfiguracao : IEntityTypeConfiguration<SessaoTreinamento>
{
    public void Configure(EntityTypeBuilder<SessaoTreinamento> b)
    {
        b.HasOne<IntegracaoAtendimento>().WithMany().HasForeignKey(x => x.IntegracaoAtendimentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IntegracaoAtendimentoId).HasFilter("[IntegracaoAtendimentoId] IS NOT NULL");
    }
}

public class FotoHashSessaoTreinamentoConfiguracao : IEntityTypeConfiguration<FotoEvidenciaSessaoTreinamento>
{
    public void Configure(EntityTypeBuilder<FotoEvidenciaSessaoTreinamento> b)
    {
        b.Property(x => x.FotoHash).HasMaxLength(64);
        b.HasIndex(x => x.FotoHash).HasFilter("[FotoHash] IS NOT NULL");
    }
}

public class NaoAssinouEtapaIntegracaoConfiguracao : IEntityTypeConfiguration<NaoAssinouEtapaIntegracao>
{
    public void Configure(EntityTypeBuilder<NaoAssinouEtapaIntegracao> b)
    {
        b.ToTable("NaoAssinouEtapaIntegracao");
        b.Property(x => x.Motivo).HasMaxLength(300).IsRequired();
        b.HasIndex(x => new { x.IntegracaoEtapaId, x.TrabalhadorId }).IsUnique().HasFilter("[Ativo] = 1");
        b.HasOne<IntegracaoEtapa>().WithMany().HasForeignKey(x => x.IntegracaoEtapaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Trabalhador>().WithMany().HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TokenCapturaFotoConfiguracao : IEntityTypeConfiguration<TokenCapturaFoto>
{
    public void Configure(EntityTypeBuilder<TokenCapturaFoto> b)
    {
        b.ToTable("TokensCapturaFoto");
        b.Property(x => x.Token).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();
        b.HasOne<SessaoTreinamento>().WithMany().HasForeignKey(x => x.SessaoTreinamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
