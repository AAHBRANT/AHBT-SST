using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Configuracoes;

public class CameraConfiguracao : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> builder)
    {
        builder.ToTable("Cameras");
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        builder.HasOne(c => c.Obra).WithMany()
            .HasForeignKey(c => c.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AreaSst).WithMany()
            .HasForeignKey(c => c.AreaSstId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ObraId, c.Nome });
        builder.HasOne(c => c.EquipamentoCamera).WithMany()
            .HasForeignKey(c => c.EquipamentoCameraId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(c => c.Fonte).HasDefaultValue(FonteCamera.Navegador);
        builder.Property(c => c.IntervaloEnvioSegundos).HasDefaultValue(3);
        builder.Property(c => c.SituacaoConexao).HasDefaultValue(SituacaoConexaoCamera.Desconhecida);
        builder.Property(c => c.MensagemConexao).HasMaxLength(300);
        builder.HasIndex(c => c.EquipamentoCameraId);
    }
}

public class OcorrenciaEpiCameraConfiguracao : IEntityTypeConfiguration<OcorrenciaEpiCamera>
{
    public void Configure(EntityTypeBuilder<OcorrenciaEpiCamera> builder)
    {
        builder.ToTable("OcorrenciasEpiCamera");
        builder.Property(o => o.Foto).IsRequired();
        builder.HasOne(o => o.Camera).WithMany()
            .HasForeignKey(o => o.CameraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Trabalhador).WithMany()
            .HasForeignKey(o => o.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.ObraId, o.RecebidaEmUtc });
        builder.HasIndex(o => new { o.CameraId, o.RecebidaEmUtc });
    }
}

public class AgenteCameraConfiguracao : IEntityTypeConfiguration<AgenteCamera>
{
    public void Configure(EntityTypeBuilder<AgenteCamera> builder)
    {
        builder.ToTable("AgentesCamera");
        builder.Property(a => a.Nome).IsRequired().HasMaxLength(100);
        builder.Property(a => a.SegredoHash).IsRequired().HasMaxLength(128);
        builder.Property(a => a.Versao).HasMaxLength(30);
        builder.HasOne(a => a.Obra).WithMany()
            .HasForeignKey(a => a.ObraId).OnDelete(DeleteBehavior.Restrict);
        // Nome único por obra entre os agentes em uso: o de um agente revogado pode ser reaproveitado.
        builder.HasIndex(a => new { a.ObraId, a.Nome }).IsUnique().HasFilter("[Ativo] = 1");
    }
}

public class EquipamentoCameraConfiguracao : IEntityTypeConfiguration<EquipamentoCamera>
{
    public void Configure(EntityTypeBuilder<EquipamentoCamera> builder)
    {
        builder.ToTable("EquipamentosCamera");
        builder.Property(e => e.Nome).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Host).IsRequired().HasMaxLength(200);
        builder.Property(e => e.EnderecoMac).HasMaxLength(17);
        builder.Property(e => e.Usuario).HasMaxLength(100);
        builder.Property(e => e.SenhaCifrada).HasMaxLength(800);
        builder.Property(e => e.Fabricante).HasMaxLength(100);
        builder.Property(e => e.Modelo).HasMaxLength(100);
        builder.HasOne(e => e.Obra).WithMany()
            .HasForeignKey(e => e.ObraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AgenteCamera).WithMany()
            .HasForeignKey(e => e.AgenteCameraId).OnDelete(DeleteBehavior.Restrict);
        // Nome único por obra entre os equipamentos em uso: o de um excluído pode ser reaproveitado.
        builder.HasIndex(e => new { e.ObraId, e.Nome }).IsUnique().HasFilter("[Ativo] = 1");
        builder.HasIndex(e => e.AgenteCameraId);
    }
}

public class TesteConexaoCameraConfiguracao : IEntityTypeConfiguration<TesteConexaoCamera>
{
    public void Configure(EntityTypeBuilder<TesteConexaoCamera> builder)
    {
        builder.ToTable("TestesConexaoCamera");
        builder.Property(t => t.Motivo).HasMaxLength(300);
        builder.Property(t => t.Detalhe).HasMaxLength(100);
        builder.Property(t => t.Codigo).HasMaxLength(40);
        // Miniatura de pessoa possível: cifrada como as demais fotos (LGPD, ver FotoBiometricaCriptografiaConversor).
        builder.Property(t => t.Miniatura).HasConversion(new FotoBiometricaCriptografiaConversor(), FotoBiometricaCriptografiaConversor.Comparador);
        // Sem FK para Obra/Camera/Equipamento: registro transitório, consultado também pela rota anônima do agente sem JOIN.
        builder.HasIndex(t => new { t.AgenteCameraId, t.SolicitadoEmUtc });
    }
}
