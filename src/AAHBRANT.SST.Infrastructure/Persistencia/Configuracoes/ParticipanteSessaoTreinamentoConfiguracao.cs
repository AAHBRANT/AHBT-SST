using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

// Evidência da presença na turma (05/11): mesma proteção da foto de assinatura (DocumentoSignatario) —
// a imagem do rosto / da digital é cifrada no banco (LGPD).
public class ParticipanteSessaoTreinamentoConfiguracao : IEntityTypeConfiguration<ParticipanteSessaoTreinamento>
{
    public void Configure(EntityTypeBuilder<ParticipanteSessaoTreinamento> builder)
    {
        builder.Property(p => p.EvidenciaConteudo).HasConversion(new FotoBiometricaCriptografiaConversor(), FotoBiometricaCriptografiaConversor.Comparador);
        builder.Property(p => p.EvidenciaContentType).HasMaxLength(80);
        builder.Property(p => p.EvidenciaHash).HasMaxLength(64);
        builder.Property(p => p.IpAddress).HasMaxLength(64);
        builder.Property(p => p.UserAgent).HasMaxLength(300);
        builder.Property(p => p.ValidacaoModelo).HasMaxLength(60);
        builder.Property(p => p.ValidacaoGrupoId).HasMaxLength(80);
        builder.Property(p => p.ValidacaoRequisicaoId).HasMaxLength(80);
    }
}
